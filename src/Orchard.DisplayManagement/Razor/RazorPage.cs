﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Orchard.DisplayManagement.Layout;
using Orchard.DisplayManagement.Shapes;
using System;
using System.Threading.Tasks;
using Orchard.DisplayManagement.Title;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Collections.Generic;

namespace Orchard.DisplayManagement.Razor
{
    public abstract class RazorPage<TModel> : Microsoft.AspNetCore.Mvc.Razor.RazorPage<TModel>
    {
        private dynamic _displayHelper;
        private IShapeFactory _shapeFactory;

        private void EnsureDisplayHelper()
        {
            if (_displayHelper == null)
            {
                IDisplayHelperFactory _factory = ViewContext.HttpContext.RequestServices.GetService<IDisplayHelperFactory>();
                _displayHelper = _factory.CreateHelper(ViewContext);
            }
        }

        private void EnsureShapeFactory()
        {
            if (_shapeFactory == null)
            {
                _shapeFactory = ViewContext.HttpContext.RequestServices.GetService<IShapeFactory>();
            }
        }

        /// <summary>
        /// Gets a dynamic shape factory to create new shapes.
        /// </summary>
        /// <example>
        /// Usage:
        /// <code>
        /// New.MyShape()
        /// New.MyShape(A: 1, B: "Some text")
        /// New.MyShape().A(1).B("Some text")
        /// </code>
        /// </example>
        public dynamic New
        {
            get
            {
                EnsureShapeFactory();
                return _shapeFactory;
            }
        }

        /// <summary>
        /// Gets an <see cref="IShapeFactory"/> to create new shapes.
        /// </summary>
        public IShapeFactory Factory
        {
            get
            {
                EnsureShapeFactory();
                return _shapeFactory;
            }
        }

        /// <summary>
        /// Renders a shape.
        /// </summary>
        /// <param name="shape">The shape.</param>
        public IHtmlContent Display(dynamic shape)
        {
            EnsureDisplayHelper();
            return (IHtmlContent)_displayHelper(shape);
        }

        private dynamic _themeLayout;
        public dynamic ThemeLayout
        {
            get
            {
                if (_themeLayout == null)
                {
                    var layoutAccessor = ViewContext.HttpContext.RequestServices.GetService<ILayoutAccessor>();

                    if (layoutAccessor == null)
                    {
                        throw new InvalidOperationException("Could not find a valid layout accessor");
                    }

                    _themeLayout = layoutAccessor.GetLayout();
                }

                return _themeLayout;
            }

            set
            {
                _themeLayout = value;
            }
        }

        private IPageTitleBuilder _pageTitleBuilder;
        public IPageTitleBuilder Title
        {
            get
            {
                if (_pageTitleBuilder == null)
                {
                    _pageTitleBuilder = ViewContext.HttpContext.RequestServices.GetRequiredService<IPageTitleBuilder>();
                }

                return _pageTitleBuilder;
            }
        }

        private IViewLocalizer _t;

        /// <summary>
        /// The <see cref="IViewLocalizer"/> instance for the current view.
        /// </summary>
        public IViewLocalizer T
        {
            get
            {
                if (_t == null)
                {
                    _t = ViewContext.HttpContext.RequestServices.GetRequiredService<IViewLocalizer>();
                    ((IViewContextAware)_t).Contextualize(this.ViewContext);
                }

                return _t;
            }
        }

        /// <summary>
        /// Renders the title segments.
        /// </summary>
        /// <returns>And <see cref="IHtmlContent"/> representing the title.</returns>
        public IHtmlContent RenderTitleSegments()
        {
            return Title.GenerateTitle();
        }

        public IHtmlContent RenderTitleSegments(string segment, string position = "0")
        {
            return RenderTitleSegments(new HtmlString(HtmlEncoder.Encode(segment)), position);
        }

        /// <summary>
        /// Adds a segment to the title and returns all segments.
        /// </summary>
        /// <param name="segment">The segment to add to the title.</param>
        /// <param name="position">Optional. The position of the segment in the title.</param>
        /// <returns>And <see cref="IHtmlContent"/> instance representing the full title.</returns>
        public IHtmlContent RenderTitleSegments(IHtmlContent segment, string position = "0")
        {
            Title.AddSegment(segment);
            return RenderTitleSegments();
        }

        /// <summary>
        /// Adds some segments to the title and returns all segments.
        /// </summary>
        /// <param name="segments">The segments to add to the title.</param>
        /// <param name="position">Optional. The position of the segments in the title.</param>
        /// <returns>And <see cref="IHtmlContent"/> instance representing the full title.</returns>
        public IHtmlContent RenderTitleSegments(IEnumerable<IHtmlContent> segments, string position = "0")
        {
            Title.AddSegments(segments);
            return RenderTitleSegments();
        }

        /// <summary>
        /// Renders the content zone of the layout.
        /// </summary>
        protected IHtmlContent RenderLayoutBody()
        {
            var result = base.RenderBody();
            return result;
        }

        /// <summary>
        /// Creates a <see cref="TagBuilder"/> to render a shape.
        /// </summary>
        /// <param name="shape">The shape.</param>
        /// <returns>A new <see cref="TagBuilder"/>.</returns>
        protected TagBuilder Tag(dynamic shape)
        {
            return Shape.GetTagBuilder(shape);
        }

        protected TagBuilder Tag(dynamic shape, string tag)
        {
            return Shape.GetTagBuilder(shape, tag);
        }

        protected new IHtmlContent RenderBody()
        {
            // Overrides the base RenderBody method to render the Layout's Content zone instead.

            return Display(ThemeLayout.Content);
        }

        /// <summary>
        /// Renders a zone from the layout.
        /// </summary>
        /// <param name="name">The name of the zone to render.</param>
        /// <param name="required">Whether the zone is required or not.</param>
        public new Task<IHtmlContent> RenderSectionAsync(string name, bool required)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var zone = ThemeLayout[name];

            if (required && zone != null && !zone.Items.Any())
            {
                throw new InvalidOperationException("Zone not found: " + name);
            }

            IHtmlContent result = Display(zone);
            return Task.FromResult(result);
        }
    }

    public abstract class RazorPage : RazorPage<dynamic>
    {
    }
}
