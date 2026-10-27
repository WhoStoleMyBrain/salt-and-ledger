# Local saves and compatibility

This is a pre-release game. Only current **schema5** saves are supported: Expanding Economy v3, economic rules1, pooled storage2 and itinerary1, with Grand Commission or Sandbox. Schemas1–4 and retired prototype modes are intentionally unsupported. There is no migration or command replay. Current schema5 content/configuration fingerprints must also match; future incompatible rule changes may invalidate saves explicitly.

The host offers three explicit local slots under `Application.persistentDataPath`. Continue/Load restores the selected saved state without advancing time. Save preserves exact identity and commission, all accounts and physical stock, fractional demand residuals, FIFO slices, every ship/voyage/policy cursor and daily budget, facility schedules/construction, households, fees/fiscal reviews, contracts, treasury/Mint and terminal state.

Loading validates the complete bounded file into a fresh world before replacing the active session. Unsupported, damaged, inconsistent or impossible states show an error and retain the active world, selection and playback. Slot writes use atomic replacement. Never edit a save to repair an economy or silently replace a failed load with a new game.

Retry Same World starts the original identity at day 0 and clears host observation; it does not continue a saved position. Text size remains a separate PlayerPrefs preference. Diagnostic exports are development-only, uniquely named bounded files under `Exports`, and do not replace slots or mutate the world. Raw private playtest saves must not be published.
