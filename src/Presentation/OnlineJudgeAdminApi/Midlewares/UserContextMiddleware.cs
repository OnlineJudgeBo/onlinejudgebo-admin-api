using System.IdentityModel.Tokens.Jwt;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdminApi.Controllers.Midlewares
{
    public class UserContextMiddleware
    {
        private readonly RequestDelegate _next;

        public UserContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var jwtToken = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last(); // Obtener el token JWT del encabezado de autorización

            if (!string.IsNullOrEmpty(jwtToken))
            {
                var roles = GetRolesFromToken(jwtToken);

                var userContext = new Roles
                {
                    UserId = GetUserIdFromToken(jwtToken),
                    RoleList = roles
                };

                context.Items["UserContext"] = userContext;
            }

            await _next(context);
        }

        private List<string> GetRolesFromToken(string jwtToken)
        {
            var roles = new List<string>();

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwtToken);

            if (token.Payload.ContainsKey("roles"))
            {
                string rolesString = token.Payload["roles"].ToString();
                roles = rolesString.Split(',').ToList();
            }

            return roles;
        }

        private string GetUserIdFromToken(string jwtToken)
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwtToken);

            return token.Subject;
        }
    }
}
