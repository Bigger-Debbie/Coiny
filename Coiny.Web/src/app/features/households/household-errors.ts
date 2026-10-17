export function householdError(value: unknown, fallback: string) {
  const response = value as { error?: { detail?: string }; status?: number };
  return response.error?.detail ?? (response.status === 429 ? 'Too many attempts. Wait a minute before trying again.' : fallback);
}
