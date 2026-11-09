# 212000 validation

Run the solution tests to validate the current implementation:

```powershell
dotnet test .\RecordingWithSourceAwareTriggers.slnx
```

The tests exercise the shipped XML catalog and transitions, distinguish
same-name triggers from different sources, reject invalid configurations, and
cover the recorder's transition and permission behavior. See the [README](README.md)
for details.
