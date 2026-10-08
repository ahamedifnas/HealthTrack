using System.Security.Claims;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Appointments;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = "Admin,Trainer,Client,HealthcareProfessional")]
[AutoValidateAntiforgeryToken]
public class AppointmentsController(ApplicationDbContext database, AppointmentClock clock, NotificationService notifications) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private IQueryable<Appointment> VisibleAppointments()
    {
        var query = database.Appointments.AsQueryable();
        if (User.IsInRole(AppRoles.Admin)) return query;
        return User.IsInRole(AppRoles.Trainer)
            ? query.Where(x => x.TrainerId == UserId)
            : User.IsInRole(AppRoles.HealthcareProfessional) ? query.Where(x => x.HealthcareProfessionalId == UserId)
            : query.Where(x => x.ClientId == UserId);
    }

    private IQueryable<ApplicationUser> AvailableTrainers() => database.Users.Where(x => x.IsActive
        && database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Trainer))
        && !database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && (r.Name == AppRoles.Admin || r.Name == AppRoles.HealthcareProfessional))));

    public async Task<IActionResult> Index()
    {
        var appointments = await VisibleAppointments().AsNoTracking().Include(x => x.Client).Include(x => x.Trainer).Include(x => x.HealthcareProfessional).ThenInclude(x => x!.User)
            .OrderByDescending(x => x.AppointmentDate).ThenByDescending(x => x.AppointmentTime).ThenByDescending(x => x.Id).ToListAsync();
        var admin = User.IsInRole(AppRoles.Admin);
        var trainer = User.IsInRole(AppRoles.Trainer) || User.IsInRole(AppRoles.HealthcareProfessional);
        return View(new AppointmentListViewModel(appointments,
            admin ? "All appointments (read-only)." : trainer ? "Appointments assigned to you." : "Your appointment requests and history.",
            !admin && !trainer && User.IsInRole(AppRoles.Client), clock.TimeZoneId));
    }

    public async Task<IActionResult> Details(int id)
    {
        var appointment = await VisibleAppointments().AsNoTracking().Include(x => x.Client).Include(x => x.Trainer).Include(x => x.HealthcareProfessional).ThenInclude(x => x!.User)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (appointment is null) return NotFound();
        var admin = User.IsInRole(AppRoles.Admin);
        var trainer = !admin && (User.IsInRole(AppRoles.Trainer) || User.IsInRole(AppRoles.HealthcareProfessional));
        var client = !admin && !trainer && User.IsInRole(AppRoles.Client);
        var future = appointment.AppointmentDate.ToDateTime(appointment.AppointmentTime) > clock.Now;
        return View(new AppointmentDetailsViewModel
        {
            Appointment = appointment, TimeZoneId = clock.TimeZoneId, IsAdmin = admin,
            CanClientCancel = client && appointment.Status == AppointmentStatus.Pending,
            CanManage = trainer && CanReschedule(appointment),
            CanApprove = trainer && appointment.Status == AppointmentStatus.Pending && future,
            CanComplete = trainer && appointment.Status == AppointmentStatus.Approved && !future,
            AwaitingScheduledTime = trainer && appointment.Status == AppointmentStatus.Approved && future,
            CanClientEdit = client && appointment.Status == AppointmentStatus.Pending && future,
            CanAdminDelete = admin && CanAdminDelete(appointment)
        });
    }

    [Authorize(Policy = FeaturePolicies.Client), HttpGet]
    public async Task<IActionResult> Book()
    {
        var model = new BookAppointmentForm { AppointmentDate = DateOnly.FromDateTime(clock.Now.AddDays(1)) };
        await PopulateTrainers(model);
        return View(model);
    }

    [Authorize(Policy = FeaturePolicies.Client), HttpPost]
    public async Task<IActionResult> Book(BookAppointmentForm model)
    {
        ValidateFutureSchedule(model);
        await ValidateProvider(model);
        if (ModelState.IsValid)
        {
            var appointment = new Appointment
            {
                ClientId = UserId, TrainerId = model.TrainerId, HealthcareProfessionalId = model.HealthcareProfessionalId,
                AppointmentDate = model.AppointmentDate!.Value, AppointmentTime = model.AppointmentTime!.Value,
                Reason = model.Reason.Trim()
            };
            database.Appointments.Add(appointment);
            notifications.Appointment(appointment, appointment.ProviderId, "requested");
            if (await SaveAppointment())
            {
                TempData["Success"] = "Appointment requested. It is pending provider approval.";
                return RedirectToAction(nameof(Details), new { id = appointment.Id });
            }
        }
        await PopulateTrainers(model);
        return View(model);
    }

    [Authorize(Policy = FeaturePolicies.Provider), HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var appointment = await database.Appointments.FirstOrDefaultAsync(x => x.Id == id && ((x.TrainerId == UserId && User.IsInRole(AppRoles.Trainer)) || (x.HealthcareProfessionalId == UserId && User.IsInRole(AppRoles.HealthcareProfessional))));
        if (appointment is null) return NotFound();
        if (!CanReschedule(appointment)) return StateError(id, "Only Pending or Approved appointments can be updated.");
        return View(new EditAppointmentForm
        {
            AppointmentDate = appointment.AppointmentDate, AppointmentTime = appointment.AppointmentTime,
            Notes = appointment.Notes, RowVersion = Convert.ToBase64String(appointment.RowVersion)
        });
    }

    [Authorize(Policy = FeaturePolicies.Provider), HttpPost]
    public async Task<IActionResult> Edit(int id, EditAppointmentForm model)
    {
        var appointment = await database.Appointments.FirstOrDefaultAsync(x => x.Id == id && ((x.TrainerId == UserId && User.IsInRole(AppRoles.Trainer)) || (x.HealthcareProfessionalId == UserId && User.IsInRole(AppRoles.HealthcareProfessional))));
        if (appointment is null) return NotFound();
        if (!CanReschedule(appointment)) return StateError(id, "Only Pending or Approved appointments can be updated.");
        ValidateFutureSchedule(model);
        byte[] version;
        try { version = Convert.FromBase64String(model.RowVersion ?? string.Empty); }
        catch (FormatException) { return BadRequest(); }
        if (version.Length != 8) return BadRequest();
        if (!appointment.RowVersion.SequenceEqual(version))
            return StateError(id, "This appointment changed while you were editing. Review it and try again.");
        if (ModelState.IsValid)
        {
            var rescheduled = appointment.AppointmentDate != model.AppointmentDate || appointment.AppointmentTime != model.AppointmentTime;
            appointment.AppointmentDate = model.AppointmentDate!.Value;
            appointment.AppointmentTime = model.AppointmentTime!.Value;
            appointment.Notes = model.Notes?.Trim();
            if (rescheduled) notifications.Appointment(appointment, appointment.ClientId, "rescheduled");
            if (await SaveAppointment())
            {
                TempData["Success"] = "Appointment updated. Its approval status has been preserved.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        return View(model);
    }

    [Authorize(Policy = FeaturePolicies.Client), HttpGet, ActionName("Cancel")]
    public async Task<IActionResult> ConfirmCancel(int id)
    {
        var appointment = await database.Appointments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ClientId == UserId);
        if (appointment is null) return NotFound();
        if (appointment.Status != AppointmentStatus.Pending) return StateError(id, "Clients can cancel only Pending appointments.");
        return View("Confirm", new ConfirmationViewModel("Cancel appointment", "Cancel this appointment request?", "Cancel", id, "Details", id));
    }

    [Authorize(Policy = FeaturePolicies.Provider), HttpGet, ActionName("TrainerCancel")]
    public async Task<IActionResult> ConfirmTrainerCancel(int id)
    {
        var appointment = await database.Appointments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && ((x.TrainerId == UserId && User.IsInRole(AppRoles.Trainer)) || (x.HealthcareProfessionalId == UserId && User.IsInRole(AppRoles.HealthcareProfessional))));
        if (appointment is null) return NotFound();
        if (!CanReschedule(appointment)) return StateError(id, "Completed or Cancelled appointments cannot be cancelled again.");
        return View("Confirm", new ConfirmationViewModel("Cancel appointment", "Cancel this appointment?", "TrainerCancel", id, "Details", id));
    }

    [Authorize(Policy = FeaturePolicies.Client), HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var appointment = await database.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == UserId);
        if (appointment is null) return NotFound();
        if (appointment.Status != AppointmentStatus.Pending)
            return StateError(id, "Clients can cancel only Pending appointments.");
        return await ChangeStatus(appointment, AppointmentStatus.Cancelled);
    }

    [Authorize(Policy = FeaturePolicies.Provider), HttpPost]
    public async Task<IActionResult> Approve(int id)
    {
        var appointment = await database.Appointments.FirstOrDefaultAsync(x => x.Id == id && ((x.TrainerId == UserId && User.IsInRole(AppRoles.Trainer)) || (x.HealthcareProfessionalId == UserId && User.IsInRole(AppRoles.HealthcareProfessional))));
        if (appointment is null) return NotFound();
        if (appointment.Status != AppointmentStatus.Pending)
            return StateError(id, "Only Pending appointments can be approved.");
        if (appointment.AppointmentDate.ToDateTime(appointment.AppointmentTime) <= clock.Now)
            return StateError(id, "Reschedule this appointment to a future time before approving it.");
        return await ChangeStatus(appointment, AppointmentStatus.Approved);
    }

    [Authorize(Policy = FeaturePolicies.Provider), HttpPost]
    public async Task<IActionResult> TrainerCancel(int id)
    {
        var appointment = await database.Appointments.FirstOrDefaultAsync(x => x.Id == id && ((x.TrainerId == UserId && User.IsInRole(AppRoles.Trainer)) || (x.HealthcareProfessionalId == UserId && User.IsInRole(AppRoles.HealthcareProfessional))));
        if (appointment is null) return NotFound();
        if (!CanReschedule(appointment)) return StateError(id, "Completed or Cancelled appointments cannot be cancelled again.");
        return await ChangeStatus(appointment, AppointmentStatus.Cancelled);
    }

    [Authorize(Policy = FeaturePolicies.Provider), HttpPost]
    public async Task<IActionResult> Complete(int id)
    {
        var appointment = await database.Appointments.FirstOrDefaultAsync(x => x.Id == id && ((x.TrainerId == UserId && User.IsInRole(AppRoles.Trainer)) || (x.HealthcareProfessionalId == UserId && User.IsInRole(AppRoles.HealthcareProfessional))));
        if (appointment is null) return NotFound();
        if (appointment.Status != AppointmentStatus.Approved)
            return StateError(id, "Only Approved appointments can be completed.");
        if (appointment.AppointmentDate.ToDateTime(appointment.AppointmentTime) > clock.Now)
            return StateError(id, "An appointment can be completed once its scheduled time has arrived.");
        return await ChangeStatus(appointment, AppointmentStatus.Completed);
    }

    private static bool CanReschedule(Appointment appointment) =>
        appointment.Status is AppointmentStatus.Pending or AppointmentStatus.Approved;

    [Authorize(Policy = FeaturePolicies.Client), HttpGet]
    public async Task<IActionResult> EditRequest(int id)
    {
        var appointment = await database.Appointments.AsNoTracking().Include(x => x.Trainer).Include(x => x.HealthcareProfessional).ThenInclude(x => x!.User)
            .SingleOrDefaultAsync(x => x.Id == id && x.ClientId == UserId);
        if (appointment is null) return NotFound();
        if (!CanClientEdit(appointment)) return StateError(id, "Only future Pending requests can be edited.");
        var model = new BookAppointmentForm { TrainerId = appointment.TrainerId, HealthcareProfessionalId = appointment.HealthcareProfessionalId, AppointmentDate = appointment.AppointmentDate,
            AppointmentTime = appointment.AppointmentTime, Reason = appointment.Reason, RowVersion = Convert.ToBase64String(appointment.RowVersion) };
        await PrepareRequestEdit(model, appointment);
        return View("Book", model);
    }

    [Authorize(Policy = FeaturePolicies.Client), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRequest(int id, BookAppointmentForm model)
    {
        var appointment = await database.Appointments.Include(x => x.Trainer).Include(x => x.HealthcareProfessional).ThenInclude(x => x!.User)
            .SingleOrDefaultAsync(x => x.Id == id && x.ClientId == UserId);
        if (appointment is null) return NotFound();
        if (!CanClientEdit(appointment)) return StateError(id, "Only future Pending requests can be edited.");
        if (model.TrainerId != appointment.TrainerId || model.HealthcareProfessionalId != appointment.HealthcareProfessionalId) return BadRequest();
        await ValidateProvider(model);
        ValidateFutureSchedule(model);
        byte[] version;
        try { version = Convert.FromBase64String(model.RowVersion ?? ""); }
        catch (FormatException) { return BadRequest(); }
        if (version.Length != 8) return BadRequest();
        if (!version.SequenceEqual(appointment.RowVersion)) return StateError(id, "This request changed. Reload it before editing.");
        if (ModelState.IsValid)
        {
            var changed = appointment.AppointmentDate != model.AppointmentDate || appointment.AppointmentTime != model.AppointmentTime || appointment.Reason != model.Reason.Trim();
            appointment.AppointmentDate = model.AppointmentDate!.Value;
            appointment.AppointmentTime = model.AppointmentTime!.Value;
            appointment.Reason = model.Reason.Trim();
            if (changed) notifications.Appointment(appointment, appointment.ProviderId, "updated");
            if (await SaveAppointment())
            { TempData["Success"] = "Appointment request updated."; return RedirectToAction(nameof(Details), new { id }); }
        }
        await PrepareRequestEdit(model, appointment);
        return View("Book", model);
    }

    private bool CanClientEdit(Appointment appointment) => appointment.Status == AppointmentStatus.Pending
        && appointment.AppointmentDate.ToDateTime(appointment.AppointmentTime) > clock.Now;

    private Task PrepareRequestEdit(BookAppointmentForm model, Appointment appointment)
    {
        ViewData["IsEditing"] = true;
        if (appointment.TrainerId is not null) model.Trainers = [new SelectListItem(appointment.ProviderName, appointment.TrainerId)];
        else model.HealthcareProfessionals = [new SelectListItem(appointment.ProviderName, appointment.HealthcareProfessionalId)];
        return Task.CompletedTask;
    }

    // Preserve completed/past history and ongoing bookings; only cancelled future bookings are removable.
    private bool CanAdminDelete(Appointment appointment) => appointment.Status == AppointmentStatus.Cancelled
        && appointment.AppointmentDate.ToDateTime(appointment.AppointmentTime) > clock.Now;

    [Authorize(Roles = AppRoles.Admin), HttpGet, ActionName("Delete")]
    public async Task<IActionResult> ConfirmDelete(int id)
    {
        var appointment = await database.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (appointment is null) return NotFound();
        if (!CanAdminDelete(appointment)) return StateError(id, "Only cancelled future bookings can be deleted. Appointment history is retained.");
        return View("Confirm", new ConfirmationViewModel("Delete cancelled appointment",
            "Permanently delete this cancelled future booking? Existing notifications will be retained.", "Delete", id, "Details", id));
    }

    [Authorize(Roles = AppRoles.Admin), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var appointment = await database.Appointments.SingleOrDefaultAsync(x => x.Id == id);
        if (appointment is null) return NotFound();
        if (!CanAdminDelete(appointment)) return StateError(id, "Only cancelled future bookings can be deleted. Appointment history is retained.");
        database.Appointments.Remove(appointment);
        if (await SaveAppointment())
        { TempData["Success"] = "Cancelled appointment deleted."; return RedirectToAction(nameof(Index)); }
        return StateError(id, "This appointment changed. Reload it before deleting.");
    }

    private IActionResult StateError(int id, string message)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<IActionResult> ChangeStatus(Appointment appointment, AppointmentStatus status)
    {
        appointment.Status = status;
        if (status == AppointmentStatus.Approved) notifications.Appointment(appointment, appointment.ClientId, "approved");
        if (status == AppointmentStatus.Cancelled) notifications.Appointment(appointment,
            UserId == appointment.ClientId ? appointment.ProviderId : appointment.ClientId, "cancelled");
        if (await SaveAppointment()) TempData["Success"] = $"Appointment {status.ToString().ToLowerInvariant()}.";
        else TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage));
        return RedirectToAction(nameof(Details), new { id = appointment.Id });
    }

    private async Task<bool> SaveAppointment()
    {
        try
        {
            await database.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "This appointment was changed by another request. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            ModelState.AddModelError(string.Empty, "The provider or client already has a Pending/Approved appointment at that time. Choose another time.");
        }
        return false;
    }

    private void ValidateFutureSchedule(AppointmentScheduleForm model)
    {
        if (model.AppointmentDate is not { } date || model.AppointmentTime is not { } time) return;
        if (date.ToDateTime(time) <= clock.Now)
            ModelState.AddModelError(nameof(model.AppointmentDate), "Choose a future date and time.");
        if (time.Ticks % TimeSpan.TicksPerMinute != 0)
            ModelState.AddModelError(nameof(model.AppointmentTime), "Choose a time in whole minutes.");
    }

    private IQueryable<HealthcareProfessional> AvailableHealthcareProfessionals() => database.HealthcareProfessionals.Where(x => x.User.IsActive
        && database.UserRoles.Any(ur => ur.UserId == x.UserId && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.HealthcareProfessional))
        && !database.UserRoles.Any(ur => ur.UserId == x.UserId && database.Roles.Any(r => r.Id == ur.RoleId && (r.Name == AppRoles.Admin || r.Name == AppRoles.Trainer))));

    private async Task ValidateProvider(BookAppointmentForm model)
    {
        if (model.TrainerId is not null && !await AvailableTrainers().AnyAsync(x => x.Id == model.TrainerId))
            ModelState.AddModelError(nameof(model.TrainerId), "Choose an active trainer from the list.");
        if (model.HealthcareProfessionalId is not null && !await AvailableHealthcareProfessionals().AnyAsync(x => x.UserId == model.HealthcareProfessionalId))
            ModelState.AddModelError(nameof(model.HealthcareProfessionalId), "Choose an active healthcare professional from the list.");
    }

    private async Task PopulateTrainers(BookAppointmentForm model)
    {
        model.Trainers = await AvailableTrainers().OrderBy(x => x.FullName).Select(x => new SelectListItem(x.FullName + " (" + x.Email + ")", x.Id)).ToListAsync();
        model.HealthcareProfessionals = await AvailableHealthcareProfessionals().OrderBy(x => x.User.FullName)
            .Select(x => new SelectListItem(x.User.FullName + " - " + x.Specialty, x.UserId)).ToListAsync();
    }
}

