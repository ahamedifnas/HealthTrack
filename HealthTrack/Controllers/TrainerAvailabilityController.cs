using System.Security.Claims;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Appointments;
using HealthTrack.Services;
using HealthTrack.Services.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Trainer}")]
[AutoValidateAntiforgeryToken]
public class TrainerAvailabilityController(
    ApplicationDbContext database,
    ITrainerAvailabilityService availabilityService,
    AppointmentClock clock) : Controller
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsAdmin => User.IsInRole(AppRoles.Admin);

    [HttpGet]
    public async Task<IActionResult> Index(string? trainerId)
    {
        var targetTrainerId = ResolveTargetTrainerId(trainerId);
        if (string.IsNullOrEmpty(targetTrainerId))
        {
            if (IsAdmin)
            {
                var firstTrainer = await AvailableTrainers().FirstOrDefaultAsync();
                if (firstTrainer is null)
                {
                    TempData["Info"] = "No active trainers exist in the system yet.";
                    return View(new TrainerScheduleOverviewViewModel { IsAdmin = true });
                }
                targetTrainerId = firstTrainer.Id;
            }
            else
            {
                return Forbid();
            }
        }

        var trainer = await database.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == targetTrainerId);
        if (trainer is null) return NotFound();

        var schedules = await database.TrainerAvailabilities.AsNoTracking()
            .Include(s => s.Breaks)
            .Where(s => s.TrainerId == targetTrainerId)
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .ToListAsync();

        var today = DateOnly.FromDateTime(clock.Now);
        var upcomingLeaves = await database.TrainerUnavailabilities.AsNoTracking()
            .Where(u => u.TrainerId == targetTrainerId && u.EndDate >= today)
            .OrderBy(u => u.StartDate)
            .ToListAsync();

        var upcomingAppointments = await database.Appointments.AsNoTracking()
            .Include(a => a.Client)
            .Where(a => a.TrainerId == targetTrainerId
                        && a.AppointmentDate >= today
                        && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved))
            .OrderBy(a => a.AppointmentDate).ThenBy(a => a.AppointmentTime)
            .Take(10)
            .ToListAsync();

        var vm = new TrainerScheduleOverviewViewModel
        {
            TrainerId = trainer.Id,
            TrainerName = trainer.FullName,
            TrainerEmail = trainer.Email,
            IsAdmin = IsAdmin,
            WeeklySchedules = schedules,
            UpcomingLeaves = upcomingLeaves,
            UpcomingAppointments = upcomingAppointments,
            AvailableTrainers = IsAdmin ? await GetTrainerSelectListAsync(targetTrainerId) : []
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Create(string? trainerId)
    {
        var targetTrainerId = ResolveTargetTrainerId(trainerId);
        if (string.IsNullOrEmpty(targetTrainerId)) return Forbid();

        var trainer = await database.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == targetTrainerId);
        if (trainer is null) return NotFound();

        var model = new TrainerAvailabilityForm
        {
            TrainerId = targetTrainerId,
            TrainerName = trainer.FullName,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            SlotDurationMinutes = 60,
            IsActive = true,
            Trainers = IsAdmin ? await GetTrainerSelectListAsync(targetTrainerId) : []
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(TrainerAvailabilityForm model)
    {
        var targetTrainerId = ResolveTargetTrainerId(model.TrainerId);
        if (string.IsNullOrEmpty(targetTrainerId)) return Forbid();
        model.TrainerId = targetTrainerId;

        ValidateScheduleForm(model, ModelState);

        if (model.StartTime.HasValue && model.EndTime.HasValue)
        {
            var hasOverlap = await availabilityService.HasOverlappingAvailabilityAsync(
                targetTrainerId, model.DayOfWeek, model.StartTime.Value, model.EndTime.Value,
                model.EffectiveStartDate, model.EffectiveEndDate);

            if (hasOverlap)
            {
                ModelState.AddModelError(string.Empty, $"An active schedule already covers this time window on {model.DayOfWeek}. Adjust times or deactivate the existing schedule.");
            }
        }

        if (ModelState.IsValid)
        {
            var availability = new TrainerAvailability
            {
                TrainerId = targetTrainerId,
                DayOfWeek = model.DayOfWeek,
                StartTime = model.StartTime!.Value,
                EndTime = model.EndTime!.Value,
                SlotDurationMinutes = model.SlotDurationMinutes,
                IsActive = model.IsActive,
                EffectiveStartDate = model.EffectiveStartDate,
                EffectiveEndDate = model.EffectiveEndDate,
                CreatedAt = DateTime.UtcNow
            };

            if (model.Breaks != null)
            {
                foreach (var b in model.Breaks.Where(b => b.BreakStartTime.HasValue && b.BreakEndTime.HasValue))
                {
                    availability.Breaks.Add(new TrainerAvailabilityBreak
                    {
                        BreakStartTime = b.BreakStartTime!.Value,
                        BreakEndTime = b.BreakEndTime!.Value,
                        Description = b.Description?.Trim()
                    });
                }
            }

            database.TrainerAvailabilities.Add(availability);
            await database.SaveChangesAsync();

            TempData["Success"] = $"Working hours schedule for {model.DayOfWeek} created successfully.";
            return RedirectToAction(nameof(Index), new { trainerId = IsAdmin ? targetTrainerId : null });
        }

        if (IsAdmin) model.Trainers = await GetTrainerSelectListAsync(targetTrainerId);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var availability = await database.TrainerAvailabilities
            .Include(a => a.Breaks)
            .Include(a => a.Trainer)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (availability is null) return NotFound();
        if (!IsAdmin && availability.TrainerId != CurrentUserId) return Forbid();

        var model = new TrainerAvailabilityForm
        {
            Id = availability.Id,
            TrainerId = availability.TrainerId,
            TrainerName = availability.Trainer.FullName,
            DayOfWeek = availability.DayOfWeek,
            StartTime = availability.StartTime,
            EndTime = availability.EndTime,
            SlotDurationMinutes = availability.SlotDurationMinutes,
            IsActive = availability.IsActive,
            EffectiveStartDate = availability.EffectiveStartDate,
            EffectiveEndDate = availability.EffectiveEndDate,
            RowVersion = Convert.ToBase64String(availability.RowVersion),
            Breaks = availability.Breaks.Select(b => new TrainerBreakForm
            {
                Id = b.Id,
                BreakStartTime = b.BreakStartTime,
                BreakEndTime = b.BreakEndTime,
                Description = b.Description
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, TrainerAvailabilityForm model)
    {
        var availability = await database.TrainerAvailabilities
            .Include(a => a.Breaks)
            .Include(a => a.Trainer)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (availability is null) return NotFound();
        if (!IsAdmin && availability.TrainerId != CurrentUserId) return Forbid();

        ValidateScheduleForm(model, ModelState);

        byte[] version;
        try { version = Convert.FromBase64String(model.RowVersion ?? string.Empty); }
        catch (FormatException) { return BadRequest(); }

        if (!availability.RowVersion.SequenceEqual(version))
        {
            ModelState.AddModelError(string.Empty, "This schedule was modified by another request. Please reload and try again.");
        }

        if (model.StartTime.HasValue && model.EndTime.HasValue)
        {
            var hasOverlap = await availabilityService.HasOverlappingAvailabilityAsync(
                availability.TrainerId, model.DayOfWeek, model.StartTime.Value, model.EndTime.Value,
                model.EffectiveStartDate, model.EffectiveEndDate, excludeId: id);

            if (hasOverlap)
            {
                ModelState.AddModelError(string.Empty, $"An active schedule already covers this time window on {model.DayOfWeek}.");
            }
        }

        if (ModelState.IsValid)
        {
            availability.DayOfWeek = model.DayOfWeek;
            availability.StartTime = model.StartTime!.Value;
            availability.EndTime = model.EndTime!.Value;
            availability.SlotDurationMinutes = model.SlotDurationMinutes;
            availability.IsActive = model.IsActive;
            availability.EffectiveStartDate = model.EffectiveStartDate;
            availability.EffectiveEndDate = model.EffectiveEndDate;

            // Replace breaks
            database.TrainerAvailabilityBreaks.RemoveRange(availability.Breaks);
            availability.Breaks.Clear();

            if (model.Breaks != null)
            {
                foreach (var b in model.Breaks.Where(b => b.BreakStartTime.HasValue && b.BreakEndTime.HasValue))
                {
                    availability.Breaks.Add(new TrainerAvailabilityBreak
                    {
                        TrainerAvailabilityId = availability.Id,
                        BreakStartTime = b.BreakStartTime!.Value,
                        BreakEndTime = b.BreakEndTime!.Value,
                        Description = b.Description?.Trim()
                    });
                }
            }

            await database.SaveChangesAsync();
            TempData["Success"] = $"Schedule for {model.DayOfWeek} updated successfully.";
            return RedirectToAction(nameof(Index), new { trainerId = IsAdmin ? availability.TrainerId : null });
        }

        model.TrainerName = availability.Trainer.FullName;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var availability = await database.TrainerAvailabilities.FirstOrDefaultAsync(a => a.Id == id);
        if (availability is null) return NotFound();
        if (!IsAdmin && availability.TrainerId != CurrentUserId) return Forbid();

        availability.IsActive = !availability.IsActive;
        await database.SaveChangesAsync();

        TempData["Success"] = $"Schedule for {availability.DayOfWeek} is now {(availability.IsActive ? "Active" : "Inactive")}.";
        return RedirectToAction(nameof(Index), new { trainerId = IsAdmin ? availability.TrainerId : null });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var availability = await database.TrainerAvailabilities.FirstOrDefaultAsync(a => a.Id == id);
        if (availability is null) return NotFound();
        if (!IsAdmin && availability.TrainerId != CurrentUserId) return Forbid();

        var trainerId = availability.TrainerId;
        database.TrainerAvailabilities.Remove(availability);
        await database.SaveChangesAsync();

        TempData["Success"] = "Schedule deleted successfully.";
        return RedirectToAction(nameof(Index), new { trainerId = IsAdmin ? trainerId : null });
    }

    [HttpPost]
    public async Task<IActionResult> ApplyStandardSchedule(string? trainerId)
    {
        var targetTrainerId = ResolveTargetTrainerId(trainerId);
        if (string.IsNullOrEmpty(targetTrainerId)) return Forbid();

        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        var created = 0;

        foreach (var day in weekdays)
        {
            var hasExisting = await database.TrainerAvailabilities
                .AnyAsync(a => a.TrainerId == targetTrainerId && a.DayOfWeek == day && a.IsActive);

            if (!hasExisting)
            {
                var schedule = new TrainerAvailability
                {
                    TrainerId = targetTrainerId,
                    DayOfWeek = day,
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(17, 0),
                    SlotDurationMinutes = 60,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Breaks =
                    [
                        new TrainerAvailabilityBreak
                        {
                            BreakStartTime = new TimeOnly(12, 0),
                            BreakEndTime = new TimeOnly(13, 0),
                            Description = "Lunch Break"
                        }
                    ]
                };
                database.TrainerAvailabilities.Add(schedule);
                created++;
            }
        }

        if (created > 0)
        {
            await database.SaveChangesAsync();
            TempData["Success"] = $"Standard business hours (Mon-Fri 9:00 AM - 5:00 PM with lunch break) applied for {created} day(s).";
        }
        else
        {
            TempData["Info"] = "Standard schedules already exist for all weekdays.";
        }

        return RedirectToAction(nameof(Index), new { trainerId = IsAdmin ? targetTrainerId : null });
    }

    // Leave & Exception Management
    [HttpGet]
    public async Task<IActionResult> Leave(string? trainerId)
    {
        var targetTrainerId = ResolveTargetTrainerId(trainerId);
        if (string.IsNullOrEmpty(targetTrainerId)) return Forbid();

        var trainer = await database.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == targetTrainerId);
        if (trainer is null) return NotFound();

        var leaves = await database.TrainerUnavailabilities.AsNoTracking()
            .Where(u => u.TrainerId == targetTrainerId)
            .OrderByDescending(u => u.StartDate)
            .ToListAsync();

        ViewBag.TrainerId = targetTrainerId;
        ViewBag.TrainerName = trainer.FullName;
        ViewBag.IsAdmin = IsAdmin;
        ViewBag.Trainers = IsAdmin ? await GetTrainerSelectListAsync(targetTrainerId) : [];

        return View(leaves);
    }

    [HttpGet]
    public async Task<IActionResult> CreateLeave(string? trainerId)
    {
        var targetTrainerId = ResolveTargetTrainerId(trainerId);
        if (string.IsNullOrEmpty(targetTrainerId)) return Forbid();

        var trainer = await database.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == targetTrainerId);
        if (trainer is null) return NotFound();

        var today = DateOnly.FromDateTime(clock.Now);
        var model = new TrainerLeaveForm
        {
            TrainerId = targetTrainerId,
            TrainerName = trainer.FullName,
            StartDate = today.AddDays(1),
            EndDate = today.AddDays(1),
            IsFullDay = true,
            Trainers = IsAdmin ? await GetTrainerSelectListAsync(targetTrainerId) : []
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateLeave(TrainerLeaveForm model)
    {
        var targetTrainerId = ResolveTargetTrainerId(model.TrainerId);
        if (string.IsNullOrEmpty(targetTrainerId)) return Forbid();
        model.TrainerId = targetTrainerId;

        if (model.StartDate is null) ModelState.AddModelError(nameof(model.StartDate), "Start date is required.");
        if (model.EndDate is null) ModelState.AddModelError(nameof(model.EndDate), "End date is required.");
        if (model.StartDate.HasValue && model.EndDate.HasValue && model.EndDate.Value < model.StartDate.Value)
        {
            ModelState.AddModelError(nameof(model.EndDate), "End date cannot be earlier than start date.");
        }

        if (!model.IsFullDay)
        {
            if (!model.StartTime.HasValue) ModelState.AddModelError(nameof(model.StartTime), "Start time is required for partial day leave.");
            if (!model.EndTime.HasValue) ModelState.AddModelError(nameof(model.EndTime), "End time is required for partial day leave.");
            if (model.StartTime.HasValue && model.EndTime.HasValue && model.EndTime.Value <= model.StartTime.Value)
            {
                ModelState.AddModelError(nameof(model.EndTime), "End time must be after start time.");
            }
        }

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), "Reason is required.");
        }

        if (ModelState.IsValid)
        {
            var leave = new TrainerUnavailability
            {
                TrainerId = targetTrainerId,
                StartDate = model.StartDate!.Value,
                EndDate = model.EndDate!.Value,
                IsFullDay = model.IsFullDay,
                StartTime = model.IsFullDay ? null : model.StartTime,
                EndTime = model.IsFullDay ? null : model.EndTime,
                Reason = model.Reason.Trim(),
                Status = TrainerLeaveStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            database.TrainerUnavailabilities.Add(leave);
            await database.SaveChangesAsync();

            // Automatically cancel and notify affected active appointments
            var cancelledCount = await availabilityService.CancelAppointmentsDueToLeaveAsync(
                targetTrainerId, leave.StartDate, leave.EndDate, leave.StartTime, leave.EndTime, leave.IsFullDay, leave.Reason);

            TempData["Success"] = cancelledCount > 0
                ? $"Leave registered. {cancelledCount} affected appointment(s) were cancelled and clients notified."
                : "Leave registered successfully. No existing bookings were affected.";

            return RedirectToAction(nameof(Leave), new { trainerId = IsAdmin ? targetTrainerId : null });
        }

        if (IsAdmin) model.Trainers = await GetTrainerSelectListAsync(targetTrainerId);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> CancelLeave(int id)
    {
        var leave = await database.TrainerUnavailabilities.FirstOrDefaultAsync(u => u.Id == id);
        if (leave is null) return NotFound();
        if (!IsAdmin && leave.TrainerId != CurrentUserId) return Forbid();

        leave.Status = TrainerLeaveStatus.Cancelled;
        await database.SaveChangesAsync();

        TempData["Success"] = "Trainer leave period has been cancelled / revoked.";
        return RedirectToAction(nameof(Leave), new { trainerId = IsAdmin ? leave.TrainerId : null });
    }

    [HttpGet]
    public async Task<IActionResult> CheckAffectedAppointments(
        string? trainerId, string startDate, string endDate, string? startTime, string? endTime, bool isFullDay)
    {
        var targetTrainerId = ResolveTargetTrainerId(trainerId);
        if (string.IsNullOrEmpty(targetTrainerId)) return Json(new { count = 0 });

        if (!DateOnly.TryParse(startDate, out var sDate) || !DateOnly.TryParse(endDate, out var eDate))
        {
            return Json(new { count = 0 });
        }

        TimeOnly? sTime = TimeOnly.TryParse(startTime, out var st) ? st : null;
        TimeOnly? eTime = TimeOnly.TryParse(endTime, out var et) ? et : null;

        var affected = await availabilityService.GetAffectedAppointmentsAsync(targetTrainerId, sDate, eDate, sTime, eTime, isFullDay);

        return Json(new
        {
            count = affected.Count,
            appointments = affected.Select(a => new
            {
                a.Id,
                ClientName = a.Client?.FullName ?? "Unknown",
                Date = a.AppointmentDate.ToString("yyyy-MM-dd"),
                Time = a.AppointmentTime.ToString("hh:mm tt"),
                a.Reason
            })
        });
    }

    // Helper methods
    private string? ResolveTargetTrainerId(string? requestedTrainerId)
    {
        if (IsAdmin)
        {
            return !string.IsNullOrWhiteSpace(requestedTrainerId) ? requestedTrainerId : null;
        }
        return CurrentUserId;
    }

    private IQueryable<ApplicationUser> AvailableTrainers() => database.Users.Where(x => x.IsActive
        && database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Trainer))
        && !database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && (r.Name == AppRoles.Admin || r.Name == AppRoles.HealthcareProfessional))));

    private async Task<List<SelectListItem>> GetTrainerSelectListAsync(string? selectedId)
    {
        return await AvailableTrainers().OrderBy(t => t.FullName)
            .Select(t => new SelectListItem(t.FullName + " (" + t.Email + ")", t.Id, t.Id == selectedId))
            .ToListAsync();
    }

    private static void ValidateScheduleForm(TrainerAvailabilityForm model, Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState)
    {
        if (!model.StartTime.HasValue) modelState.AddModelError(nameof(model.StartTime), "Start time is required.");
        if (!model.EndTime.HasValue) modelState.AddModelError(nameof(model.EndTime), "End time is required.");

        if (model.StartTime.HasValue && model.EndTime.HasValue)
        {
            if (model.EndTime.Value <= model.StartTime.Value)
            {
                modelState.AddModelError(nameof(model.EndTime), "End time must be after start time.");
            }
            else
            {
                var totalMinutes = (model.EndTime.Value - model.StartTime.Value).TotalMinutes;
                if (model.SlotDurationMinutes <= 0 || model.SlotDurationMinutes > totalMinutes)
                {
                    modelState.AddModelError(nameof(model.SlotDurationMinutes), $"Slot duration must be between 15 and {totalMinutes} minutes.");
                }
            }
        }

        if (model.EffectiveStartDate.HasValue && model.EffectiveEndDate.HasValue && model.EffectiveEndDate.Value < model.EffectiveStartDate.Value)
        {
            modelState.AddModelError(nameof(model.EffectiveEndDate), "Effective end date cannot be earlier than start date.");
        }

        if (model.Breaks != null && model.StartTime.HasValue && model.EndTime.HasValue)
        {
            var validBreaks = model.Breaks.Where(b => b.BreakStartTime.HasValue && b.BreakEndTime.HasValue).ToList();
            for (var i = 0; i < validBreaks.Count; i++)
            {
                var b = validBreaks[i];
                var bStart = b.BreakStartTime!.Value;
                var bEnd = b.BreakEndTime!.Value;

                if (bEnd <= bStart)
                {
                    modelState.AddModelError($"Breaks[{i}].BreakEndTime", "Break end time must be after break start time.");
                }
                if (bStart < model.StartTime.Value || bEnd > model.EndTime.Value)
                {
                    modelState.AddModelError($"Breaks[{i}].BreakStartTime", "Break must be completely within working hours.");
                }
                for (var j = i + 1; j < validBreaks.Count; j++)
                {
                    var other = validBreaks[j];
                    var oStart = other.BreakStartTime!.Value;
                    var oEnd = other.BreakEndTime!.Value;

                    if (bStart < oEnd && bEnd > oStart)
                    {
                        modelState.AddModelError($"Breaks[{i}].BreakStartTime", "Breaks cannot overlap with each other.");
                    }
                }
            }
        }
    }
}
