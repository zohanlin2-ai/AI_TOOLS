---
name: schematic-analysis
description: "Analyze hardware schematic PDFs efficiently by building private per-document evidence notes with a connectivity inventory for controllers and major ICs. Use for repeated schematic questions, not for electrical sign-off."
---

# Schematic Analysis

Use this Skill for questions about hardware schematic PDFs that are likely to recur. Preserve evidence while avoiding repeated full-document visual analysis.

## Privacy and Storage

Treat the source PDF and every derived note, image, crop, text extraction, and component list as sensitive unless the user says otherwise.

- Store per-document analysis notes outside any Git repository whenever possible.
- Do not upload, commit, or otherwise publish source material or derived notes without explicit authorization.
- If the user requires a repository-local cache, require a dedicated ignore rule and remind them that force-adding can bypass ignore rules.

## Private Analysis Note

Use one private Markdown note per source PDF. Bind it to the source with its path, SHA-256, revision, page count, and review date.

Record:

- a page map;
- a functional component-connectivity inventory for every processor, MCU, SoC, CPU, FPGA, ASIC, programmable-logic device, and other major IC;
- a complete pin/function map for each primary controller or programmable device, including unused pins and peripheral-mux assignments when applicable;
- verified facts with a page reference and component or section identifier;
- inferences with their basis and confidence;
- unresolved questions; and
- a concise change log.

`Verified` means the fact was checked against the current PDF and has a specific page reference. It does not mean the circuit was electrically certified, simulated, or tested.

In the connectivity inventory, summarize a signal as a functional endpoint-to-endpoint connection (for example, `controller → I2C bus → sensor`). Do not clutter the primary table with ordinary series R/C/L parts. Record a passive component separately when it materially changes behavior, such as a pull-up/pull-down, divider, filter, termination, level conversion, power-feedback network, RF matching network, ESD path, or current-setting element.

## Build or Refresh the Note

When no matching private note exists, or its source fingerprint differs from the current PDF:

1. Inspect the complete source PDF using both text extraction and visual review.
2. Identify the primary control or programmable device(s); do not assume the device is an MCU. It may instead be a CPU, ARM-based SoC, FPGA, ASIC, or a multi-controller design.
3. Build the page map and a functional component-connectivity inventory before writing a high-level system summary.
4. For each primary controller or programmable device, record every connected I/O/pin role, peripheral-mux assignment, and intentionally unused pin. Retain package ball/pin identifiers where shown.
5. Label conclusions as `Verified`, `Inference`, or `Open question`.
6. Keep source references precise enough to re-check a relevant page without reopening the entire document.

## Answering Follow-Up Questions

1. Read the matching private analysis note first.
2. Answer from a `Verified` fact when the source fingerprint matches and the user has not requested fresh validation.
3. Re-open only the relevant source page when the note is missing, the item is an inference, the source changed, the question crosses page boundaries, or the user asks to confirm the answer.
4. State the source page for every confirmed claim and clearly distinguish inferences from evidence.
5. Re-open the complete PDF only when the question genuinely requires a whole-document review.

## Final Review of the Private Analysis Note

After completing or materially updating a circuit's private Markdown note, carefully review it again against the source PDF. Confirm that the source fingerprint, page references, controller/major-IC connectivity inventory, primary-controller pin map, `Verified` labels, inference boundaries, and recorded uncertainties are accurate before treating the note as a reusable source.
