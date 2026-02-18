using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Data;

public static class SeedEval360
{
    public static async Task EnsureAsync(ApplicationDbContext db)
    {
        // Si no existen tablas, esto va a tronar: por eso primero crea el schema en PostgreSQL (script).
        // Aquí solo sembramos catálogo inicial.
        if (await db.Eval360Competencies.AnyAsync())
            return;

        // Competencias (basadas en tu ejemplo)
        var autoconciencia = new Eval360Competency { Name = "Autoconciencia", SortOrder = 10 };
        var resultados = new Eval360Competency { Name = "Búsqueda de resultados", SortOrder = 20 };
        var liderazgo = new Eval360Competency { Name = "Liderazgo", SortOrder = 30 };

        db.Eval360Competencies.AddRange(autoconciencia, resultados, liderazgo);

        db.Eval360Questions.AddRange(
            // Autoconciencia
            new Eval360Question { CompetencyId = autoconciencia.Id, SortOrder = 10, Text = "Mantiene sus emociones y su comportamiento bajo control, incluso durante situaciones de mucha presión." },
            new Eval360Question { CompetencyId = autoconciencia.Id, SortOrder = 20, Text = "Demuestra un comportamiento ético." },
            new Eval360Question { CompetencyId = autoconciencia.Id, SortOrder = 30, Text = "Actúa con profesionalismo." },
            new Eval360Question { CompetencyId = autoconciencia.Id, SortOrder = 40, Text = "Aprende de sus errores." },

            // Búsqueda de resultados
            new Eval360Question { CompetencyId = resultados.Id, SortOrder = 10, Text = "Se centra en las necesidades del cliente." },
            new Eval360Question { CompetencyId = resultados.Id, SortOrder = 20, Text = "Soluciona problemas." },

            // Liderazgo
            new Eval360Question { CompetencyId = liderazgo.Id, SortOrder = 10, Text = "Inspira en los demás el crecimiento y el aprendizaje continuos." },
            new Eval360Question { CompetencyId = liderazgo.Id, SortOrder = 20, Text = "Maneja los conflictos de una manera adecuada." },
            new Eval360Question { CompetencyId = liderazgo.Id, SortOrder = 30, Text = "Toma la iniciativa para resolver los problemas." },
            new Eval360Question { CompetencyId = liderazgo.Id, SortOrder = 40, Text = "Motiva a los demás a alcanzar sus objetivos." }
        );

        await db.SaveChangesAsync();
    }
}
