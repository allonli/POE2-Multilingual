export type SelectedTextPreferences = {
  prefillSelectedText?: boolean;
};

export function shouldReadSelectedText(
  preferences: SelectedTextPreferences,
): boolean {
  return preferences.prefillSelectedText === true;
}
