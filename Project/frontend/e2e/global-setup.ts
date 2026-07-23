/**
 * Playwright global setup — resets the E2E database before the suite runs.
 *
 * Prerequisites:
 *   - API must be running with ASPNETCORE_ENVIRONMENT=E2E
 *   - Default port: 5079. Override with E2E_API_URL env var.
 *
 * If the reset call fails (e.g. 404 because the API is in Development mode),
 * the setup throws a descriptive error and aborts the entire test suite.
 */
async function globalSetup(): Promise<void> {
  const apiUrl = process.env['E2E_API_URL'] ?? 'http://localhost:5079'
  const resetUrl = `${apiUrl}/api/test/reset`

  let response: Response
  try {
    response = await fetch(resetUrl, { method: 'POST' })
  } catch (err) {
    throw new Error(
      `[global-setup] Could not reach API at ${resetUrl}.\n` +
        `  Cause: ${String(err)}\n` +
        `  Make sure the API is running with ASPNETCORE_ENVIRONMENT=E2E.`
    )
  }

  if (!response.ok) {
    throw new Error(
      `[global-setup] DB reset failed (${response.status}). ` +
        `Is the API running with ASPNETCORE_ENVIRONMENT=E2E?`
    )
  }
}

export default globalSetup
