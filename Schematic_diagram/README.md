# Private Schematic Analysis Workflow

Use this workflow for repeat questions about sensitive schematic PDFs while keeping analysis efficient and traceable.

## Principles

- Keep the generic method in the personal `schematic-analysis` Skill.
- Keep findings for each PDF in a separate private Markdown note.
- Use a short question prompt to select the note and define the question.
- Treat the original PDF and every derived analysis note as confidential.

## Where to Store Files

Store the per-PDF private analysis note outside the Git repository. A suitable structure is:

```text
<private-cache>/<pdf-sha256>/private-analysis.md
```

Do not place the private note in a repository that can be pushed to GitHub. A `.gitignore` rule helps prevent ordinary `git add` operations, but an out-of-repository location is the stronger safeguard.

## First-Time Analysis

1. Run the `schematic-analysis` Skill on the PDF.
2. Record the PDF path, SHA-256, revision, page count, and analysis date.
3. Create a page map that identifies the purpose of every page.
4. Identify every primary control or programmable device. Do not assume it is an MCU: it may be a CPU, ARM-based SoC, FPGA, ASIC, or another programmable-logic device.
5. Build a functional component-connectivity inventory before the system summary. Record endpoint-to-endpoint roles such as `controller → SPI → display` or `FPGA → GPIO → actuator`.
6. For each primary controller or programmable device, record the complete pin/function map: ordinary I/O, peripheral-mux assignments, and unused pins.
7. In the main connectivity table, collapse ordinary R/C/L series parts. Record them separately whenever they change circuit behavior—for example pull-ups, dividers, filters, terminations, ESD networks, current setting, level shifting, power feedback, or RF matching.
8. Separate confirmed facts from inferences and open questions.
9. Include a page reference with every recorded fact.
10. Perform the final review required by the Skill before relying on the note.

## Private Note Template

```markdown
# <Document Name> Private Analysis

## Source fingerprint
- File: <local path>
- SHA-256: <hash>
- Revision: <revision>
- Pages: <count>
- Reviewed: <date>

## Page map
- Page 1: <purpose>

## Verified facts
- <fact> - Source: page <n>, <section or component>

## Component connectivity inventory
| Main device | Pin / interface | Functional endpoint | Role | Source |
| --- | --- | --- | --- | --- |
| <controller, CPU, SoC, FPGA, or ASIC> | <pin or bus> | <component/net> | <GPIO, I2C, SPI, power, etc.> | <page> |

## Primary-device pin map
| Device | Pin | Intended function | Status | Source |
| --- | --- | --- | --- | --- |
| <device> | <pin> | <GPIO or muxed peripheral> | <used / unused> | <page> |

## Inferences
- <inference> - Basis: <observations>; confidence: <low|medium|high>

## Open questions
- <question>

## Change log
- <date>: <what was added or corrected>
```

## Asking a Follow-Up Question

Use a short prompt such as:

```text
Use the private analysis note for <PDF name> at <private path>.
Answer this question: <question>.
Use the note first. Re-open only the relevant source page when the note is missing,
the source hash has changed, the note is marked as an inference, or I ask for verification.
State the source page for every confirmed answer.
```

## Meaning of "Verified"

`Verified` means that the statement was checked against the current source PDF and is recorded with a specific page reference. It may be answered from the private note when the source fingerprint still matches.

It does **not** mean that the circuit has been electrically certified, simulated, or tested on hardware. For a new request to validate a claim, re-open the referenced page before answering.

## When to Re-Open the PDF

Re-open the relevant page, rather than the entire PDF, when:

- the private note does not cover the question;
- the recorded item is an inference or has low confidence;
- the PDF hash or revision has changed;
- the question needs a cross-page connection check; or
- the user requests a fresh verification.
