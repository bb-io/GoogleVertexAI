﻿namespace Apps.GoogleVertexAI.Utils.Xliff;

public class TransUnit
{
    public string Source { get; set; }

    public string Target { get; set; }

    public string Id { get; set; }

    public List<Tag> Tags { get; set; } 
}