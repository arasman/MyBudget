/**
 * Extracts the API error code from an Axios error response.
 *
 * Backend returns either:
 *   { error: "SCREAMING_SNAKE_CODE" }       — business rule violations
 *   { detail: "SCREAMING_SNAKE_CODE" }      — ProblemDetails shape
 *
 * Returns the string code, or null if no code can be determined.
 */
export function extractApiErrorCode(err: unknown): string | null {
  if (err == null || typeof err !== 'object') return null

  const axiosErr = err as {
    response?: {
      data?: {
        error?: string
        detail?: string
      }
    }
  }

  const data = axiosErr.response?.data
  if (!data) return null

  if (typeof data.error === 'string' && data.error) return data.error
  if (typeof data.detail === 'string' && data.detail) return data.detail

  return null
}
