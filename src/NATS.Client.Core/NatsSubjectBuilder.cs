using System.Text.RegularExpressions;

namespace NATS.Client.Core;

internal class NatsSubjectBuilder
{
    private Dictionary<string, object> _parameters = new Dictionary<string, object>();
    private Dictionary<string, SubjectDef> _subjectDefs = new Dictionary<string, SubjectDef>();

    public void AddParameter(string name, object? value)
    {
        if (value == null)
        {
            _parameters.Remove(name);
        }
        else
        {
            _parameters.Add(name, value);
        }
    }

    public void ReplaceParameters(Dictionary<string, object> parameters)
    {
        _parameters.Clear();
        _parameters = parameters;
    }

    public bool AddTemplate(string name, string template)
    {
        return AddTemplate(name, template, "USER");
    }

    public NatsSubject GenerateFromTemplate(string templateName, string? fallbackTemplateName = default)
    {
        if (_subjectDefs.TryGetValue(templateName, out var subjectDef))
        {
            return new NatsSubject()
            {
                Template = subjectDef.Template,
                Path = MakePath(subjectDef),
                Type = subjectDef.Type,
            };
        }

        throw new ArgumentNullException("Template not defined");
    }

    public NatsSubject GenerateFromPath(string path)
    {
        var parts = path.Split('.');
        var subjectDefs = _subjectDefs.Where(x => x.Value.Template.Count(x => x == '.') == parts.Length - 1).ToList();
        foreach (var subjectDef in subjectDefs)
        {
            var segments = subjectDef.Value.Template.Split('.');
            var matched = 0;
            for (var i = 0; i < segments.Length; i++)
            {
                if (!((segments[i].StartsWith("{") && segments[i].StartsWith("}")) ||
                    segments[i].Equals(parts[i])))
                {
                    // This segment is not either a placeholder or a match to the path provided
                    break;
                }

                matched++;
            }

            if (matched == segments.Length)
            {
                return new NatsSubject()
                {
                    Template = subjectDef.Value.Template,
                    Path = path,
                    Type = subjectDef.Value.Type,
                };
            }
        }

        return new NatsSubject()
        {
            Template = default,
            Path = path,
            Type = "RAW",
        };
    }

    internal bool AddTemplate(string name, string template, string type)
    {
        try
        {
            _subjectDefs.Add(name, new SubjectDef()
            {
                Name = name,
                Template = template,
                Type = type,
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private string MakePath(SubjectDef def)
    {
        var subject = def.Template;
        if (_parameters.Count == 0)
        {
            return subject;
        }

        var tokens = Regex.Matches(def.Template, "{{\\w+}}");
        foreach (Match token in tokens)
        {
            if (_parameters.TryGetValue(token.Value.Trim('{').Trim('}'), out var value))
            {
                subject.Replace(token.Value, value.ToString());
            }
        }

        return subject;
    }

    private struct SubjectDef
    {
        public string Name;
        public string Template;
        public string Type;
    }
}
