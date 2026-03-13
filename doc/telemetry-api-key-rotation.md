## Telemetry API key rotation

- **Storage**: Each garden has its own `TelemetryApiKey` stored in the management database (both in the domain model and `GardenEntity`). The telemetry webhook (`/api/v1/telemetry/readings`) looks up a garden by this key.
- **Default generation**:
  - New gardens created via the domain `Garden.Create(...)` generate a key using `Guid.NewGuid().ToString("N")`.
  - The demo garden seeded in `ManagementDbContextSeeder` also uses a randomly generated key.

### Manual rotation procedure (per garden)

1. **Identify the garden** whose telemetry key you want to rotate (e.g. by `Id` or name) in the `gm.gardens` table.
2. **Generate a new strong key** on a secure machine, for example:

   ```sql
   -- Example (PostgreSQL): generate a 32-character hex key
   SELECT encode(gen_random_bytes(16), 'hex');
   ```

3. **Update the garden row** with the new key:

   ```sql
   UPDATE "gm"."gardens"
   SET "TelemetryApiKey" = '<NEW_KEY>'
   WHERE "Id" = '<GARDEN_ID>';
   ```

4. **Redeploy or reconfigure any devices/simulators** that send telemetry for this garden so they use the new `X-Garden-Telemetry-Key` header value.
5. **Optionally audit logs/metrics** for requests using the old key to confirm that no further traffic is received.

This procedure invalidates the old key immediately for that garden while leaving other tenants unaffected.

