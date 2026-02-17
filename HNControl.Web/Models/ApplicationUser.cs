using Microsoft.AspNetCore.Identity;

namespace HNControl.Web.Models;

public class ApplicationUser : IdentityUser
{
    // Lo dejamos mínimo; datos “humanos” van en EmployeeProfile.
}
