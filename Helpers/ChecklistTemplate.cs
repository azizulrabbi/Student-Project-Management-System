namespace SPMS.Helpers
{
    public static class ChecklistTemplate
    {
        public record Item(string Key, string Label);
        public record Section(string Title, List<Item> Items);

        public static readonly List<Section> Sections = new()
        {
            new("Week 1-2: First Meeting – Introduction", new() {
                new("w1_agenda",   "Meeting agenda"),
                new("w1_minutes",  "Meeting minutes"),
                new("w1_signoff",  "Sign-off industry agreement"),
            }),
            new("Week 3: Project Proposal Submission", new() {
                new("w3_proposal", "Project Proposal submission"),
            }),
            new("Week 4-5: Second Meeting – Requirements Gathering", new() {
                new("w45_agenda",        "Meeting agenda"),
                new("w45_minutes",       "Meeting minutes"),
                new("w45_questionnaire", "Requirement Questionnaire"),
            }),
            new("Week 6: Final Requirements", new() {
                new("w6_requirements", "Final Requirements"),
            }),
            new("Week 7: Mid-Semester Feedback", new() {
                new("w7_feedback", "Mid-Semester Feedback"),
            }),
            new("Week 8-9: Third Meeting – Project Design", new() {
                new("w89_agenda",    "Meeting agenda"),
                new("w89_minutes",   "Meeting minutes"),
                new("w89_flowchart", "Flow chart and Database design"),
            }),
            new("Week 10: Wireframe and Prototype", new() {
                new("w10_wireframe", "Wireframe and Prototype"),
            }),
            new("Week 11: Fourth Meeting – Summary of Project Design", new() {
                new("w11_agenda",       "Meeting agenda"),
                new("w11_minutes",      "Meeting minutes"),
                new("w11_report",       "Project Report submission"),
                new("w11_presentation", "Project Presentation"),
                new("w11_implplan",     "Implementation plan (based on client's suggestion)"),
            }),
            new("Week 12: End-of-Semester Feedback", new() {
                new("w12_feedback", "End-of-Semester Feedback"),
            }),
        };

        public static int TotalItems => Sections.Sum(s => s.Items.Count);

        public static bool IsValidKey(string key) =>
            Sections.Any(s => s.Items.Any(i => i.Key == key));
    }
}
