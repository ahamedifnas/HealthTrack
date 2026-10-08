# HealthTrack user manual

All dates and appointment times use the time-zone label shown by the application. Use the navigation for your signed-in role. Validation messages appear above or beside forms; correct the indicated fields and submit again.

## Client

1. Choose Register, enter full name, email, password and confirmation. Registration creates a Client account. For later visits use Login. Deactivated accounts cannot sign in.
2. The Client Dashboard shows weight/BMI progress, current plan previews, upcoming approved appointment and recent notifications. Open the relevant cards for details.

[Insert Screenshot: Client Dashboard]

3. Choose Measurements ? Add measurement. Enter the date, positive weight in kg, height in cm and optional health fields. Save. One measurement per client/date is allowed. Details allows editing or deleting your own records.
4. Choose Progress to see weight/BMI history and changes. Set a target through the target-weight form. BMI is calculated from stored kg/cm values; it is not entered manually.

[Insert Screenshot: Health Measurement and Progress]

5. Choose My Workouts, open a plan and review exercises. Choose Log Workout, select a plan, load its exercises when needed, enter date, duration, calories and completion, then save. History and log details show your entries.
6. Choose My Diet, open a plan and review meal items. Choose Log Meal, select a plan and enter meal date/type, food, quantity and calories. Save and review History.

[Insert Screenshot: Workout Plan and Log]
[Insert Screenshot: Diet Plan and Meal Log]

7. Choose Book Appointment. For a trainer consultation, select an active Trainer and leave Healthcare professional empty. For a healthcare consultation, leave Trainer empty and select a Healthcare professional, listed with specialty. Enter a future date/time and reason; submit. Exactly one provider is required and a reserved provider/client time cannot be reused.
8. Appointments ? Details shows the provider, status, date/time, reason and notes. A future Pending request can be edited without changing provider. Clients may cancel Pending requests; ask the provider to change an Approved appointment. Approval is performed by the selected provider, not by the admin.

[Insert Screenshot: Trainer Appointment Booking]
[Insert Screenshot: Healthcare Professional Appointment Booking]

9. Open Notifications to read event messages and reminders. You can mark them read/unread, open details and delete them. Visiting Dashboard, Appointments or Notifications generates eligible reminders. Appointment reminders are due tomorrow and today before the scheduled time; workout/meal reminders appear once daily for currently active plans. They are in-app messages, not email or SMS. Deleted reminders do not reappear for the same delivery key.

[Insert Screenshot: Notifications and Reminders]

## Trainer

1. Sign in using the account created by an administrator. The Trainer Dashboard lists assigned-client totals, upcoming appointments and plan metrics.
2. Choose Clients to open current assigned clients and view their progress. Assignment changes take effect server-side immediately.
3. Choose Workout Plans ? Create. Select an assigned client, enter name/dates/description, save and add exercises. Use plan details to edit, change status or manage exercises. Deletion retains logs where the existing workflow requires preserving history.
4. Choose Diet Plans ? Create. Select the client, enter dates and daily calorie target, then add meal items with quantities and nutrition values.
5. Choose Appointments. Open your appointment ? Approve, Update / reschedule, Cancel, or Complete. Completion is allowed only for Approved appointments whose scheduled time has arrived. A trainer can receive a booking without a plan assignment; client health/progress access still requires assignment.
6. Choose Reports and select an assigned client for Progress, Workout, Diet or Appointments reports. Choose Engagement and popular plans for aggregate analytics. Browser print supports saving reports as PDF.

[Insert Screenshot: Trainer Dashboard]
[Insert Screenshot: Trainer Clients and Plan Management]
[Insert Screenshot: Trainer Appointments and Reports]

## Healthcare Professional

1. Sign in with the account created by the admin. Dashboard takes you to your appointment list.
2. Open Details for a consultation. Use Approve for a future Pending request.
3. Use Update / reschedule to enter a future date/time and notes. Save. If somebody has changed the appointment, reload before trying again.
4. Use Cancel and confirm to cancel a Pending or Approved appointment.
5. Use Complete after an Approved appointment's scheduled time has arrived. Completed and Cancelled appointments are terminal.
6. Open Notifications for booking/reschedule/cancellation messages and appointment reminders. Healthcare professionals cannot access client measurements, workout/diet management, or trainer/admin reports through this role.

[Insert Screenshot: Healthcare Professional Appointments]
[Insert Screenshot: Healthcare Consultation Details]

## Administrator

1. Sign in to the Admin Dashboard. Review totals, recent appointments, trainers and assignments.
2. Choose Users or Clients for account details/editing. The existing workflow protects your own account and the last active administrator from deactivation and preserves dependent history.
3. Add Trainers from the dashboard; edit details or activate/deactivate them with the existing controls.
4. Choose Healthcare Professionals ? Add healthcare professional. Enter name, email, specialty and password/confirmation. Edit updates profile/account fields and Active status. Deactivation prevents login and new selection while retaining past appointments.
5. Use the assignment form on the Admin Dashboard to assign an active client to an active trainer. Remove/reassign through the existing controls. A client has at most one current trainer assignment.
6. Choose Reports for system-wide totals, popularity and engagement. Printable Report includes the analytics tables.
7. Choose Appointments for the system-wide overview and Details. Admin access does not approve or complete provider appointments; the existing deletion workflow only permits cancelled future records.

[Insert Screenshot: Admin Dashboard and Assignments]
[Insert Screenshot: Healthcare Professional Administration]
[Insert Screenshot: Admin Reports and Appointment Overview]
