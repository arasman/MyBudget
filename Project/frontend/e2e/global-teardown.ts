/**
 * Playwright global teardown — resets the E2E database after the suite completes.
 * Failures are non-fatal: a warning is logged but the suite result is not affected.
 */
async function globalTeardown(): Promise<void> {
  const apiUrl = process.env['E2E_API_URL'] ?? 'http://localhost:5079'
  const resetUrl = `${apiUrl}/api/test/reset`

  try {
    const response = await fetch(resetUrl, { method: 'POST' })
    if (!response.ok) {
      console.warn(
        `[global-teardown] DB reset returned ${response.status}. ` +
          `The E2E database may not be clean for the next run.`
      )
    }
  } catch (err) {
    console.warn(
      `[global-teardown] Could not reach API at ${resetUrl}. Skipping teardown reset.\n` +
        `  Cause: ${String(err)}`
    )
  }
}

export default globalTeardown
