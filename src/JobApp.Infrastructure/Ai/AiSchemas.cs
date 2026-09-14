namespace JobApp.Infrastructure.Ai;

using System.Text.Json;

/// <summary>
/// JSON Schemas handed to the Messages API via <c>OutputConfig.Format</c>, so the
/// model is constrained to emit parseable output instead of us regexing prose.
/// </summary>
internal static class AiSchemas
{
    /// <summary>Deserialises a schema literal into the shape the SDK wants.</summary>
    private static Dictionary<string, JsonElement> Parse(string json)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    /// <summary>Work-model classification plus fit scoring for one job.</summary>
    public static Dictionary<string, JsonElement> Triage { get; } = Parse(
        """
        {
          "type": "object",
          "additionalProperties": false,
          "required": [
            "workModel", "workModelConfidence", "workModelEvidence",
            "skillScore", "seniorityScore", "matchedSkills", "missingMustHaves",
            "missingResumeKeywords", "redFlags", "rationale"
          ],
          "properties": {
            "workModel": {
              "type": "string",
              "enum": ["Remote", "Hybrid", "Onsite", "Unknown"],
              "description": "Unknown is a valid and expected answer when the posting genuinely does not say."
            },
            "officeDaysPerWeek": {
              "type": ["integer", "null"],
              "minimum": 0,
              "maximum": 5,
              "description": "Only when the posting states a number. Null otherwise - do not guess."
            },
            "workModelConfidence": {
              "type": "number",
              "minimum": 0,
              "maximum": 1,
              "description": "How certain you are about workModel. Be honest; low values are useful."
            },
            "workModelEvidence": {
              "type": ["string", "null"],
              "description": "Short verbatim quote from the posting that justifies workModel."
            },
            "skillScore": {
              "type": "integer",
              "minimum": 0,
              "maximum": 100,
              "description": "How well the candidate's skills match what this job requires."
            },
            "seniorityScore": {
              "type": "integer",
              "minimum": 0,
              "maximum": 100,
              "description": "Seniority and years-of-experience fit. Penalise both under- and over-qualification."
            },
            "requiredYearsExperience": {
              "type": ["integer", "null"],
              "minimum": 0,
              "maximum": 50
            },
            "mentionsVisaSponsorship": {
              "type": ["boolean", "null"],
              "description": "Null when the posting is silent on sponsorship."
            },
            "matchedSkills": {
              "type": "array",
              "items": { "type": "string" },
              "maxItems": 20,
              "description": "Skills required by the job that the candidate demonstrably has."
            },
            "missingMustHaves": {
              "type": "array",
              "items": { "type": "string" },
              "maxItems": 10,
              "description": "Profile must-have skills this posting does not ask for, or job requirements the candidate lacks."
            },
            "missingResumeKeywords": {
              "type": "array",
              "items": { "type": "string" },
              "maxItems": 20,
              "description": "Terms prominent in the posting but absent from the resume - the ATS gap."
            },
            "redFlags": {
              "type": "array",
              "items": { "type": "string" },
              "maxItems": 8,
              "description": "Concerns: vague scope, contract-to-hire, unpaid, staffing-agency reposts, title inflation."
            },
            "rationale": {
              "type": "string",
              "maxLength": 700,
              "description": "Two or three sentences explaining the scores."
            }
          }
        }
        """);

    /// <summary>Resume tailoring output: summary, skills line, per-bullet edits.</summary>
    public static Dictionary<string, JsonElement> Tailoring { get; } = Parse(
        """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["tailoredSummary", "tailoredSkillsLine", "keywordsToAdd", "honestyNotes", "suggestions"],
          "properties": {
            "tailoredSummary": {
              "type": "string",
              "description": "Rewritten professional summary aimed at this job. Same facts, different emphasis."
            },
            "tailoredSkillsLine": {
              "type": "string",
              "description": "The candidate's real skills, reordered so what this job asks for reads first."
            },
            "keywordsToAdd": {
              "type": "array",
              "items": { "type": "string" },
              "maxItems": 20,
              "description": "Terms from the posting the candidate can honestly claim but has not written down."
            },
            "honestyNotes": {
              "type": "string",
              "description": "What you deliberately did NOT claim because the resume does not support it. Required - never leave empty."
            },
            "coverLetterDraft": {
              "type": ["string", "null"],
              "description": "Only when a cover letter was requested."
            },
            "suggestions": {
              "type": "array",
              "maxItems": 25,
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["section", "originalText", "suggestedText", "rationale", "keywordsAdded"],
                "properties": {
                  "section": {
                    "type": "string",
                    "enum": ["Summary", "Skills", "Experience", "Projects", "Education", "Other"]
                  },
                  "sectionHeading": {
                    "type": ["string", "null"],
                    "description": "Employer or heading the bullet sits under, copied from the resume."
                  },
                  "originalText": {
                    "type": "string",
                    "description": "The existing bullet verbatim. Empty string when proposing a brand-new bullet."
                  },
                  "suggestedText": { "type": "string" },
                  "rationale": {
                    "type": "string",
                    "maxLength": 300,
                    "description": "Why this helps against THIS posting specifically."
                  },
                  "keywordsAdded": {
                    "type": "array",
                    "items": { "type": "string" },
                    "maxItems": 8
                  }
                }
              }
            }
          }
        }
        """);

    /// <summary>Fields recovered from a pasted job description or fetched page.</summary>
    public static Dictionary<string, JsonElement> JobParsing { get; } = Parse(
        """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["title", "company", "description"],
          "properties": {
            "title": { "type": "string" },
            "company": { "type": "string" },
            "location": { "type": ["string", "null"] },
            "city": { "type": ["string", "null"] },
            "countryCode": {
              "type": ["string", "null"],
              "description": "Lowercase ISO 3166-1 alpha-2, e.g. in, us, gb."
            },
            "description": {
              "type": "string",
              "description": "The full job description as plain text, boilerplate and navigation stripped."
            },
            "postedAt": {
              "type": ["string", "null"],
              "description": "ISO 8601 date if the page states one. Null if it only says 'X days ago' and you cannot resolve it."
            },
            "postedDaysAgo": {
              "type": ["integer", "null"],
              "description": "Use this when the page says 'posted 3 days ago' instead of a date."
            },
            "salaryMin": { "type": ["number", "null"] },
            "salaryMax": { "type": ["number", "null"] },
            "salaryCurrency": { "type": ["string", "null"] }
          }
        }
        """);
}
