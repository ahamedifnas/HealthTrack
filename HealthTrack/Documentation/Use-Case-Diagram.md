# Use cases

Mermaid flowchart notation represents actors and use cases; the application implements the actions shown.

~~~mermaid
flowchart LR
    C[Client] --> Login([Register / Login])
    T[Trainer] --> SignIn([Login / Logout])
    H[Healthcare Professional] --> SignIn
    A[Administrator] --> SignIn
    C --> CD([View client dashboard])
    C --> Health([Manage own measurements and target; view progress])
    C --> Logs([View own plans; log workouts and meals])
    C --> Book([Book one Trainer OR Healthcare Professional])
    C --> Request([View / edit future pending request; cancel pending request])
    C --> Notify([Read and manage own notifications])
    T --> Notify
    H --> Notify
    T --> TD([View trainer dashboard and assigned clients])
    T --> Plans([Manage assigned-client workout / diet plans])
    T --> Reports([View assigned-client reports and analytics])
    T --> Manage([Approve / reschedule / cancel / complete own appointments])
    H --> Manage
    A --> AD([View admin dashboard])
    A --> Users([Manage users and trainers])
    A --> HP([Create / edit / activate healthcare professionals])
    A --> Assign([Assign / remove / reassign trainer-client relationship])
    A --> Global([View system reports, analytics and appointment overview])
    Notify --> Reminder([Generate eligible in-app reminders on relevant visits])
~~~

Providers manage appointments selected for them. Trainer report/health access additionally requires a current assignment. Healthcare professionals do not use trainer reports or health-measurement screens. Admin is not a substitute provider for approvals/completion.
