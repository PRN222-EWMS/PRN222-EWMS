# Authentication and Admin accounts — 2026-10-28

Implemented username/password login, POST logout, and Admin-only account listing and creation in the MVC repository/service/controller structure. Existing entity mappings and SQL schema remain compatible. Views follow the warm canvas, restrained borders, and blue actions in DESIGN-notion.md.

Authentication uses ASP.NET Core Identity password hashes and cookies. Account status and role are rechecked on authenticated requests. Creation validates duplicate usernames/emails, role IDs, and warehouse assignment. SQL seed password placeholders are rejected. An explicitly enabled bootstrap can initialize the first Admin without replacing an existing real password hash.

Review found lost Vietnamese characters from shell encoding and hidden password validation errors; both were corrected. Build passed with zero warnings/errors. Seventeen checks against real services and a fake repository passed, including password verification, hashing, duplicates, role validation, and warehouse validation. HTTP checks reported in the implementation plan cover login rendering, anonymous redirects, antiforgery, required fields, and the forbidden page.

Database-backed login and account creation were not exercised end-to-end because an existing Admin password was unavailable. Service tests used a temporary console project outside the application source tree; NuGet vulnerability-feed lookup emitted a network warning, but execution succeeded.

Reviewed docs/authentication.md against the implementation and completed plan. No commit was required or performed.

Account creation now excludes ADMIN from selectable roles and rejects forged ADMIN requests in the service. Only WAREHOUSE_STAFF can select a warehouse; changing roles clears the field, and nonstaff accounts always save a null warehouse. Bootstrap checks for an existing Admin regardless of status and uses a serializable transaction to prevent duplicate initialization. Twenty-nine focused service checks and six form behavior checks passed; isolated project build passed with zero warnings/errors. Existing database records were untouched. Admin Windows Modify permission was granted on the new JavaScript file to preserve Visual Studio save access.

Removed the duplicate static workspace selector above the sidebar navigation and its unused responsive styles. The single workspace group contains Overview and Notifications. Razor/project build passed with zero warnings/errors.

Profile now renders warehouse details only for WAREHOUSE_STAFF. Password changes return the saved password version to the controller, which renews the current identity cookie using the original authentication properties and redirects to Profile with a success notice. Other stale-session cookies remain rejected. Real MVC HTTP checks passed for retained authentication, persistent-cookie expiry, old/new passwords, warehouse visibility and invalid-input feedback; isolated build passed with zero warnings/errors. No production database writes were needed for verification.

Redesigned login against the supplied screenshot with a split introduction/form layout, native HTML/SVG document illustrations, the existing package logo and a blue sign-in action. Removed policy/duration/support notes and added no code comments. Login-only CSS preserves the compact AccessDenied view. Fourteen MVC HTTP checks passed; isolated headless Chrome verified password visibility and no horizontal overflow at 320, 390, 768, 1024 and 1664 pixels. Desktop and mobile screenshots were inspected. Readable description contrast meets AA, and isolated build passed with zero warnings/errors. The temporary test host was stopped and Windows Admin editing rights were preserved on the new stylesheet.
