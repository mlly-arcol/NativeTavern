# NativeTavern UI Design QA

## Comparison target

- Source visual truth 1: `C:\Users\tang\AppData\Local\Temp\codex-clipboard-d6182dbc-fea3-4038-8218-eeb733d41fa7.png`
- Source visual truth 2: `C:\Users\tang\AppData\Local\Temp\codex-clipboard-adf6c8f6-5eaa-4fb2-947f-f883d117add9.png`
- Chat implementation: `design-qa-chat.png`
- Prompt Studio implementation: `design-qa-prompt-studio.png`
- Side-by-side evidence: `design-qa-chat-comparison.png`, `design-qa-prompt-studio-comparison.png`
- Viewport: desktop WPF window, approximately 1266 x 813 logical pixels at 100% capture scale.
- Source pixels: 1270 x 815 (Chat), 1269 x 811 (Prompt Studio).
- Implementation pixels: 1266 x 813 for both screens.
- Density normalization: source images were normalized to 1266 x 813 only for the side-by-side comparison; original implementation captures were retained unchanged.
- State: empty New Chat with Provider warning; Prompt Studio Persona tab with empty resource list.

## Full-view comparison evidence

- Chat: the circled prompt-context toolbar is reduced from roughly 58 px controls with 9 px vertical container padding to 32 px controls with 5 px vertical padding. The entire content surface is white, text is black, and controls use light-gray separation.
- Prompt Studio: the legacy bordered tab strip and unstructured form were replaced with a compact segmented tab control, a dedicated resource-list card, and a separate detail form card with aligned actions.
- Overall: sidebar, page background, cards, fields, selected states, warning surfaces, and composer now form one consistent light theme.

## Focused-region comparison evidence

Separate crops were not needed because both user-marked regions remain clearly legible at original resolution in the saved side-by-side comparison images. The toolbar height, tab treatment, form boundaries, button hierarchy, and text contrast are directly visible there.

## Required fidelity surfaces

- Fonts and typography: Segoe UI Variable / Segoe UI is preserved. Black primary text, gray supporting text, semibold headings, and compact 12–14 px control labels provide a clear hierarchy without wrapping in the marked regions.
- Spacing and layout rhythm: toolbar height is reduced by about one third; 7–18 px gaps, 8–12 px radii, aligned card edges, and right-aligned destructive/primary actions replace the earlier oversized and disconnected layout.
- Colors and visual tokens: main background is `#FFFFFF`, sidebar `#F7F7F8`, controls `#F3F4F6`, primary text `#171717`, and borders `#DADDE1`. Primary actions use black with white text; destructive actions use a pale red surface and red text.
- Image quality and asset fidelity: no image assets are required by either marked area. Existing logo/avatar treatment remains sharp and code-native at the captured desktop density.
- Copy and content: existing feature labels and bindings are retained. Prompt Studio descriptions were shortened and clarified; no functional field or action was removed.

## Interaction verification

- Chat, Prompt Studio, and sidebar navigation were opened in the running Windows build.
- Personas, Lorebooks, and Prompt presets tabs were clicked and rendered correctly.
- Existing commands and bindings for creation, selection, save, delete, context apply, and clear remain connected.
- No WPF load or binding-blocking error appeared during the checked flows.

## Comparison history

1. Initial implementation exposed one P1 contrast issue: black primary buttons inherited black text.
2. Fix: button content foreground inheritance was corrected and the global TextBlock foreground override was removed so primary actions inherit white text.
3. Post-fix evidence: `design-qa-chat.png` shows a legible white “Open settings” label; `design-qa-prompt-studio.png` shows a legible white “Save persona” label.

## Findings

- No actionable P0, P1, or P2 issues remain in the requested regions.
- P3: the interface intentionally retains some English labels alongside the existing Chinese chat welcome copy; full localization was outside this request.

## Implementation checklist

- [x] Reduce marked toolbar control height by approximately one third.
- [x] Redesign Prompt Studio tabs, resource pane, form pane, and action hierarchy.
- [x] Convert application surfaces to white backgrounds with black text.
- [x] Verify all three Prompt Studio tabs in the running Windows app.
- [x] Run build and automated tests.

final result: passed
