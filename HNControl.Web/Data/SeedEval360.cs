using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Data;

public static class SeedEval360
{
    public static async Task EnsureAsync(ApplicationDbContext db)
    {
        // Si no existen tablas, aquí tronaría: primero corre el SQL de schema.
        // Sembrado idempotente: crea/activa lo que falta y desactiva lo que sobra.

        var desired = new (string Comp, int CompSort, List<string> Questions)[]
        {
            ("Liderazgo", 10, new List<string>
            {
                "¿Confía y delega responsabilidades en su equipo?",
                "¿Es capaz de tomar decisiones difíciles y responsabilizarse de los resultados?",
                "¿Otros miembros del equipo lo buscan para que los ayude con su trabajo?",
                "¿Es capaz de influir y persuadir a los demás para lograr los objetivos del equipo?",
                "¿Motiva y mantiene la moral alta en el grupo de trabajo?"
            }),
            ("Comunicación", 20, new List<string>
            {
                "¿Se comunica de forma clara y concisa con compañeros de trabajo o clientes?",
                "Si no entiende algo, ¿pregunta hasta comprender la información?",
                "¿Se comunica de manera eficaz por escrito, con buena gramática y ortografía?",
                "¿Escucha las sugerencias de los demás?",
                "¿Crea oportunidades para el diálogo?"
            }),
            ("Resolución de problemas", 30, new List<string>
            {
                "¿Reconoce cuando hay un problema?",
                "¿Sugiere soluciones útiles para resolver el problema?",
                "¿Entiende los impactos de una dificultad laboral?",
                "¿Es capaz de solucionar por sí mismo un problema?",
                "¿Se detiene a evaluar todas las posibles soluciones antes de resolver un problema?"
            }),
            ("Eficiencia", 40, new List<string>
            {
                "¿Cómo prioriza sus tareas?",
                "¿Completa sus tareas de manera efectiva?",
                "¿Supera las expectativas en su trabajo?",
                "¿Cómo aprovecha los recursos para cumplir con los objetivos?",
                "¿Mejora los procesos para hacerlos más eficientes?"
            })
        };

        var desiredNames = desired.Select(x => x.Comp).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var comps = await db.Eval360Competencies.Include(c => c.Questions).ToListAsync();

        // 1) Crear / activar competencias deseadas
        foreach (var (compName, compSort, _) in desired)
        {
            var comp = comps.FirstOrDefault(c => c.Name.Equals(compName, StringComparison.OrdinalIgnoreCase));
            if (comp == null)
            {
                comp = new Eval360Competency { Name = compName, SortOrder = compSort, IsActive = true };
                db.Eval360Competencies.Add(comp);
                comps.Add(comp);
            }
            else
            {
                comp.Name = compName;
                comp.SortOrder = compSort;
                comp.IsActive = true;
            }
        }

        // 2) Desactivar competencias viejas que no están en el set actual
        foreach (var c in comps)
        {
            if (!desiredNames.Contains(c.Name))
                c.IsActive = false;
        }

        await db.SaveChangesAsync();

        // Refrescar con IDs ya persistidos
        comps = await db.Eval360Competencies.Include(c => c.Questions).ToListAsync();

        // 3) Crear / activar preguntas por competencia (y desactivar sobrantes)
        foreach (var (compName, _, questions) in desired)
        {
            var comp = comps.First(c => c.Name.Equals(compName, StringComparison.OrdinalIgnoreCase));

            var desiredQ = questions.Select(q => q.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // activar / crear
            var order = 10;
            foreach (var qText in questions)
            {
                var q = comp.Questions.FirstOrDefault(x => x.Text.Equals(qText, StringComparison.OrdinalIgnoreCase));
                if (q == null)
                {
                    q = new Eval360Question
                    {
                        CompetencyId = comp.Id,
                        Text = qText.Trim(),
                        SortOrder = order,
                        IsActive = true
                    };
                    db.Eval360Questions.Add(q);
                    comp.Questions.Add(q);
                }
                else
                {
                    q.Text = qText.Trim();
                    q.SortOrder = order;
                    q.IsActive = true;
                }
                order += 10;
            }

            // desactivar sobrantes dentro de la competencia
            foreach (var q in comp.Questions)
            {
                if (!desiredQ.Contains(q.Text))
                    q.IsActive = false;
            }
        }

        await db.SaveChangesAsync();
    }
}
