﻿using Orchard.DisplayManagement.Handlers;
using Orchard.DisplayManagement.Shapes;
using System;
using System.Threading.Tasks;

namespace Orchard.DisplayManagement.Views
{
    public class ShapeResult<TModel> : DisplayResult<TModel>
    {
        private string _defaultLocation;
        private string _differentiator;
        private string _prefix;
        private string _cacheId;
        private readonly string _shapeType;
        private readonly Func<BuildShapeContext<TModel>, dynamic> _shapeBuilder;
        private readonly Func<dynamic, Task> _processing;
        private Action<ShapeMetadataCacheContext> _cache;
        private string _groupId;

        public ShapeResult(string shapeType, Func<BuildShapeContext<TModel>, dynamic> shapeBuilder)
            :this(shapeType, shapeBuilder, null)
        {
        }

        public ShapeResult(string shapeType, Func<BuildShapeContext<TModel>, dynamic> shapeBuilder, Func<dynamic, Task> processing)
        {
            // The shape type is necessary before the shape is created as it will drive the placement
            // resolution which itself can prevent the shape from being created.

            _shapeType = shapeType;
            _shapeBuilder = shapeBuilder;
            _processing = processing;
        }

        public override void Apply(BuildDisplayContext<TModel> context)
        {
            ApplyImplementation(context, context.DisplayType);
        }

        public override void Apply(BuildEditorContext<TModel> context)
        {
            ApplyImplementation(context, null);
        }

        private void ApplyImplementation(BuildShapeContext<TModel> context, string displayType)
        {
            var placement = context.FindPlacement(_shapeType, _differentiator, _defaultLocation);
            if (String.IsNullOrEmpty(placement.Location) || placement.Location == "-")
            {
                return;
            }

            // Parse group placement.
            _groupId = placement.GetGroup();

            if (!String.Equals(context.GroupId ?? "", _groupId ?? "", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var newShape = _shapeBuilder(context);

            // Ignore it if the driver returned a null shape.
            if (newShape == null)
            {
                return;
            }
            
            ShapeMetadata newShapeMetadata = newShape.Metadata;
            newShapeMetadata.Prefix = _prefix;
            newShapeMetadata.DisplayType = displayType;
            newShapeMetadata.PlacementSource = placement.Source;
            newShapeMetadata.Tab = placement.GetTab();

            // The _processing callback is used to delay execution of costly initialization
            // that can be prevented by caching
            if(_processing != null)
            {
                newShapeMetadata.OnProcessing(_processing);
            }

            // Apply cache settings
            if(!String.IsNullOrEmpty(_cacheId) && _cache != null)
            {
                _cache(newShapeMetadata.Cache(_cacheId));
            }

            // If a specific shape is provided, remove all previous alternates and wrappers.
            if (!String.IsNullOrEmpty(placement.ShapeType))
            {
                newShapeMetadata.Type = placement.ShapeType;
                newShapeMetadata.Alternates.Clear();
                newShapeMetadata.Wrappers.Clear();
            }

            foreach (var alternate in placement.Alternates)
            {
                newShapeMetadata.Alternates.Add(alternate);
            }

            foreach (var wrapper in placement.Wrappers)
            {
                newShapeMetadata.Wrappers.Add(wrapper);
            }

            dynamic parentShape = context.Shape;

            if(placement.IsLayoutZone())
            {
                parentShape = context.Layout;
            }

            var position = placement.GetPosition();
            var zones = placement.GetZones();

            foreach(var zone in zones)
            {
                parentShape = parentShape.Zones[zone];
            }

            if (String.IsNullOrEmpty(position))
            {
                parentShape.Add(newShape);
            }
            else
            {
                parentShape.Add(newShape, position);
            }
        }

        public ShapeResult<TModel> Prefix(string prefix)
        {
            _prefix = prefix;
            return this;
        }

        public ShapeResult<TModel> Location(string zone)
        {
            _defaultLocation = zone;
            return this;
        }

        public ShapeResult<TModel> Differentiator(string differentiator)
        {
            _differentiator = differentiator;
            return this;
        }

        public ShapeResult<TModel> OnGroup(string groupId)
        {
            _groupId = groupId;
            return this;
        }

        public ShapeResult<TModel> Cache(string cacheId, Action<ShapeMetadataCacheContext> cache = null)
        {
            _cacheId = cacheId;
            _cache = cache;
            return this;
        }

        public string GetDifferentiator()
        {
            return _differentiator;
        }

        public string GetGroup()
        {
            return _groupId;
        }

        public string GetLocation()
        {
            return _defaultLocation;
        }

        public string GetShapeType()
        {
            return _shapeType;
        }
    }
}