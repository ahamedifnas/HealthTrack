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

[Authorize(Roles = "Admin,Trainer,Client")]
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
            : query.Where(x => x.ClientId == UserId);
    }

    private IQueryable<ApplicationUser> AvailableTrainers() => database.Users.Where(x => x.IsActive
        && database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Trainer))
        && !database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Admin)));

    public async Task<IActionResult> Index()
    {
        var appointments = await VisibleAppointments().AsNoTracking().Include(x => x.Client).Include(x => x.Trainer)
            .OrderByDescending(x => x.AppointmentDate).ThenByDescending(x => x.AppointmentTime).ThenByDescending(x => x.Id).ToListAsync();
        var admin = User.IsInRole(AppRoles.Admin);
        var trainer = User.IsInRole(AppRoles.Trainer);
        return View(new AppointmentListViewModel(appointments,
            admin ? "All appointments (read-only)." : trainer ? "Appointments assigned to you." : "Your appointment requests and history.",
            !admin && !trainer && User.IsInRole(AppRoles.Client), clock.TimeZoneId));
    }

    public async Task<IActionResult> Details(int id)
    {
        var appointment = await VisibleAppointments().AsNoTracking().Include(x => x.Client).Include(x => x.Trainer)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (appointment is null) return NotFound();
        var admin = User.IsInRole(AppRoles.Admin);
        var trainer = !admin && User.IsInRole(AppRoles.Trainer);
        var client = !admin && !trainer && User.IsInRole(AppRoles.Client);
        var future = appointment.AppointmentDate.ToDateTime(appointment.AppointmentTime) > clock.Now;
        return View(new AppointmentDetailsViewModel
        {
            Appointment = appointment, TimeZoneId = clock.TimeZoneId, IsAdmin = admin,
            CanClientCancel = client && appointment.Status == AppointmentStatus.Pending,
            CanManage = trainer && CanReschedule(appointment),
            CanApprove = trainer && appointment.Status == AppointmentStatus.Pending && future,
            CanComplete = trainer && appointment.Status == AppointmentStatus.Approved && !future,
            AwaitingScheduledTime = trainer && appointment.Status == AppointmentStatus.Approved && future
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
        if (!await AvailableTrainers().AnyAsync(x => x.Id == model.TrainerId))
            ModelState.AddModelError(nameof(model.TrainerId), "Choose an active trainer from the list.");
        if (ModelState.IsValid)
        {
            var appointment = new Appointment
            {
                ClientId = UserId, TrainerId = model.TrainerId,
                AppointmentDate = model.AppointmentDate!.Value, AppointmentTime = model.AppointmentTime!.Value,
                Reason = model.Reason.Trim()
            };
            database.Appointments.Add(appointment);
            notifications.Appointment(appointment, appointment.TrainerId, "requested");
            if (await SaveAppointment())
            {
                TempData["Success"] = "Appointment requested. It is pending trainer approval.";
                return RedirectToAction(nameof(Details), new { id = appointment.Id });
            }
        }
        await PopulateTrainers(model);
        return View(model);
    }

    [Authorize(Policy = FeaturePolicies.Trainer), HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var appointment = await database.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.TrainerId == UserId);
        if (appointment is null) return NotFound();
        if (!CanReschedule(appointment)) return StateError(id, "Only Pending or Approved appointments can be updated.");
        return View(new EditAppointmentForm
        {
            AppointmentDate = appointment.AppointmentDate, AppointmentTime = appointment.AppointmentTime,
            Notes = appointment.Notes, RowVersion = Convert.ToBase64String(appointment.RowVersion)
        });
    }

    [Authorize(Policy = FeaturePolicies.Trainer), HttpPost]
    public async Task<IActionResult> Edit(int id, EditAppointmentForm model)
    {
        var appointment = await database.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.TrainerId == UserId);
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

    [Authorize(Policy = FeaturePolicies.Trainer), HttpGet, ActionName("TrainerCancel")]
    public async Task<IActionResult> ConfirmTrainerCancel(int id)
    {
        var appointment = await database.Appointments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.TrainerId == UserId);
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

    [Authorize(Policy = FeaturePolicies.Trainer), HttpPost]
    public async Task<IActionResult> Approve(int id)
    {
        var appointment = await database.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.TrainerId == UserId);
        if (appointment is null) return NotFound();
        if (appointment.Status != AppointmentStatus.Pending)
            return StateError(id, "Only Pending appointments can be approved.");
        if (appointment.AppointmentDate.ToDateTime(appointment.AppointmentTime) <= clock.Now)
            return StateError(id, "Reschedule this appointment to a future time before approving it.");
        return await ChangeStatus(appointment, AppointmentStatus.Approved);
    }

    [Authorize(Policy = FeaturePolicies.Trainer), HttpPost]
    public async Task<IActionResult> TrainerCancel(int id)
    {
        var appointment = await database.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.TrainerId == UserId);
        if (appointment is null) return NotFound();
        if (!CanReschedule(appointment)) return StateError(id, "Completed or Cancelled appointments cannot be cancelled again.");
        return await ChangeStatus(appointment, AppointmentStatus.Cancelled);
    }

    [Authorize(Policy = FeaturePolicies.Trainer), HttpPost]
    public async Task<IActionResult> Complete(int id)
    {
        var appointment = await database.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.TrainerId == UserId);
        if (appointment is null) return NotFound();
        if (appointment.Status != AppointmentStatus.Approved)
            return StateError(id, "Only Approved appointments can be completed.");
        if (appointment.AppointmentDate.ToDateTime(appointment.AppointmentTime) > clock.Now)
            return StateError(id, "An appointment can be completed once its scheduled time has arrived.");
        return await ChangeStatus(appointment, AppointmentStatus.Completed);
    }

    private static bool CanReschedule(Appointment appointment) =>
        appointment.Status is AppointmentStatus.Pending or AppointmentStatus.Approved;

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
            UserId == appointment.ClientId ? appointment.TrainerId : appointment.ClientId, "cancelled");
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
            ModelState.AddModelError(string.Empty, "The trainer or client already has a Pending/Approved appointment at that time. Choose another time.");
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

    private async Task PopulateTrainers(BookAppointmentForm model) => model.Trainers = await AvailableTrainers()
        .OrderBy(x => x.FullName).Select(x => new SelectListItem(x.FullName + " (" + x.Email + ")", x.Id)).ToListAsync();
}

