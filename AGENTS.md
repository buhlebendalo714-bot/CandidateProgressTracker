# Working rules

- Preserve the Domain/Application/Infrastructure/Web architecture.
- Enforce authorization in services as well as Razor Pages.
- Preserve existing data, migrations, audit records, and unrelated changes.
- Never reset or delete the development database.
- Tests may create and delete only their uniquely named temporary databases.
- Review generated migrations before applying them to the development database.
- Run focused tests for each change. Do not repeatedly run the full suite.
- Do not commit, push, or deploy unless explicitly requested.
- Finish authorized implementation and verification without repeatedly asking for confirmation. Report changes, test results, and unresolved limitations.
