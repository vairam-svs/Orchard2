﻿using System.Threading.Tasks;
using Orchard.ContentManagement.Metadata.Models;
using Orchard.DependencyInjection;
using Orchard.DisplayManagement.Handlers;
using Orchard.DisplayManagement.Views;

namespace Orchard.ContentManagement.Display.ContentDisplay
{
    public interface IContentPartDisplayDriver : IDependency
    {
        Task<IDisplayResult> BuildDisplayAsync(ContentPart contentPart, ContentTypePartDefinition typePartDefinition, BuildDisplayContext context);
        Task<IDisplayResult> BuildEditorAsync(ContentPart contentPart, ContentTypePartDefinition typePartDefinition, BuildEditorContext context);
        Task<IDisplayResult> UpdateEditorAsync(ContentPart contentPart, ContentTypePartDefinition typePartDefinition, UpdateEditorContext context);
    }
}
