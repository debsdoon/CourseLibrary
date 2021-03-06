using AutoMapper;
using CourseLibrary.API.DbContexts;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Serialization;
using Microsoft.OpenApi.Models;
using System;
using System.IO;
using System.Reflection;

namespace CourseLibrary.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers(setupAction =>
            {
                setupAction.ReturnHttpNotAcceptable = true;

            }).AddNewtonsoftJson(setupAction =>
             {
                 setupAction.SerializerSettings.ContractResolver =
                    new CamelCasePropertyNamesContractResolver();
             })
             .AddXmlDataContractSerializerFormatters()
            .ConfigureApiBehaviorOptions(setupAction =>
            {
                setupAction.InvalidModelStateResponseFactory = context =>
                {
                    // create a problem details object
                    var problemDetailsFactory = context.HttpContext.RequestServices
                        .GetRequiredService<ProblemDetailsFactory>();
                    var problemDetails = problemDetailsFactory.CreateValidationProblemDetails(
                            context.HttpContext, 
                            context.ModelState); 

                    // add additional info not added by default
                    problemDetails.Detail = "See the errors field for details.";
                    problemDetails.Instance = context.HttpContext.Request.Path;

                    // find out which status code to use
                    var actionExecutingContext =
                          context as Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext;

                    // if there are modelstate errors & all keys were correctly
                    // found/parsed we're dealing with validation errors
                    //
                    // if the context couldn't be cast to an ActionExecutingContext
                    // because it's a ControllerContext, we're dealing with an issue 
                    // that happened after the initial input was correctly parsed.  
                    // This happens, for example, when manually validating an object inside
                    // of a controller action.  That means that by then all keys
                    // WERE correctly found and parsed.  In that case, we're
                    // thus also dealing with a validation error.
                    if (context.ModelState.ErrorCount > 0 &&
                        (context is ControllerContext ||
                         actionExecutingContext?.ActionArguments.Count == context.ActionDescriptor.Parameters.Count))
                    {
                        problemDetails.Type = "https://courselibrary.com/modelvalidationproblem";
                        problemDetails.Status = StatusCodes.Status422UnprocessableEntity;
                        problemDetails.Title = "One or more validation errors occurred.";

                        return new UnprocessableEntityObjectResult(problemDetails)
                        {
                            ContentTypes = { "application/problem+json" }
                        };
                    }

                    // if one of the keys wasn't correctly found / couldn't be parsed
                    // we're dealing with null/unparsable input
                    problemDetails.Status = StatusCodes.Status400BadRequest;
                    problemDetails.Title = "One or more errors on input occurred.";
                    return new BadRequestObjectResult(problemDetails)
                    {
                        ContentTypes = { "application/problem+json" }
                    };
                };
            });

            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            services.AddScoped<ICourseLibraryRepository, CourseLibraryRepository>();

            services.AddDbContext<CourseLibraryContext>(options =>
            {
                options.UseSqlServer(
                    @"Server=(localdb)\mssqllocaldb;Database=CourseLibraryDB;Trusted_Connection=True;");
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Course Library API", 
                                                    Version = "v1",
                                                    Description = "Through this API you can access authors and their books.",
                                                    Contact = new Microsoft.OpenApi.Models.OpenApiContact()
                                                    {
                                                        Email = "devendra@gmail.com",
                                                        Name = "Devendra S Rawat",
                                                        Url = new Uri("https://www.twitter.com/debsdoon")
                                                    },
                                                    License = new Microsoft.OpenApi.Models.OpenApiLicense()
                                                    {
                                                        Name = "MIT License",
                                                        Url = new Uri("https://opensource.org/licenses/MIT")
                                                    }
                });
                var xmlCommentsFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlCommentsFullPath = Path.Combine(AppContext.BaseDirectory, xmlCommentsFile);

                c.IncludeXmlComments(xmlCommentsFullPath);
            });

            //services.AddSwaggerGen(setupAction =>
            //{
            //    setupAction.OperationFilter<GetBookOperationFilter>();
            //    setupAction.OperationFilter<CreateBookOperationFilter>();

            //    foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
            //    {
            //        setupAction.SwaggerDoc($"LibraryOpenAPISpecification{description.GroupName}",
            //            new Microsoft.OpenApi.Models.OpenApiInfo()
            //            {
            //                Title = "Library API",
            //                Version = description.ApiVersion.ToString(),
            //                Description = "Through this API you can access authors and their books.",
            //                Contact = new Microsoft.OpenApi.Models.OpenApiContact()
            //                {
            //                    Email = "kevin.dockx@gmail.com",
            //                    Name = "Kevin Dockx",
            //                    Url = new Uri("https://www.twitter.com/KevinDockx")
            //                },
            //                License = new Microsoft.OpenApi.Models.OpenApiLicense()
            //                {
            //                    Name = "MIT License",
            //                    Url = new Uri("https://opensource.org/licenses/MIT")
            //                }
            //            });

            //    }

            //    setupAction.DocInclusionPredicate((documentName, apiDescription) =>
            //    {
            //        var actionApiVersionModel = apiDescription.ActionDescriptor
            //        .GetApiVersionModel(ApiVersionMapping.Explicit | ApiVersionMapping.Implicit);

            //        if (actionApiVersionModel == null)
            //        {
            //            return true;
            //        }

            //        if (actionApiVersionModel.DeclaredApiVersions.Any())
            //        {
            //            return actionApiVersionModel.DeclaredApiVersions.Any(v =>
            //            $"LibraryOpenAPISpecificationv{v.ToString()}" == documentName);
            //        }
            //        return actionApiVersionModel.ImplementedApiVersions.Any(v =>
            //            $"LibraryOpenAPISpecificationv{v.ToString()}" == documentName);
            //    });

            //    var xmlCommentsFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            //    var xmlCommentsFullPath = Path.Combine(AppContext.BaseDirectory, xmlCommentsFile);

            //    setupAction.IncludeXmlComments(xmlCommentsFullPath);
            //});
        }


        internal static IActionResult ProblemDetailsInvalidModelStateResponse(
            ProblemDetailsFactory problemDetailsFactory, ActionContext context)
        {
            var problemDetails = problemDetailsFactory.CreateValidationProblemDetails(context.HttpContext, context.ModelState);
            ObjectResult result;
            if (problemDetails.Status == 400)
            {
                // For compatibility with 2.x, continue producing BadRequestObjectResult instances if the status code is 400.
                result = new BadRequestObjectResult(problemDetails);
            }
            else
            {
                result = new ObjectResult(problemDetails);
            }
            result.ContentTypes.Add("application/problem+json");
            result.ContentTypes.Add("application/problem+xml");

            return result;
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Course Library API V1");
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(appBuilder =>
                {
                    appBuilder.Run(async context =>
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("An unexpected fault happened. Try again later.");
                    });
                });

            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
