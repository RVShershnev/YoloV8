﻿using Microsoft.ML.OnnxRuntime.Tensors;

using Compunet.YoloV8.Data;
using Compunet.YoloV8.Metadata;
using Compunet.YoloV8.Extensions;

namespace Compunet.YoloV8.Parsers;

internal readonly struct DetectionOutputParser
{
    private readonly YoloV8Metadata _metadata;
    private readonly YoloV8Parameters _parameters;

    public DetectionOutputParser(YoloV8Metadata metadata, YoloV8Parameters parameters)
    {
        _metadata = metadata;
        _parameters = parameters;
    }

    public IReadOnlyList<IBoundingBox> Parse(Tensor<float> output, Size origin)
    {
        var metadata = _metadata;
        var parameters = _parameters;

        var reductionRatio = Math.Min(metadata.ImageSize.Width / (float)origin.Width, metadata.ImageSize.Height / (float)origin.Height);

        var xPadding = (metadata.ImageSize.Width - origin.Width * reductionRatio) / 2;
        var yPadding = (metadata.ImageSize.Height - origin.Height * reductionRatio) / 2;

        var magnificationRatio = Math.Max((float)origin.Width / metadata.ImageSize.Width, (float)origin.Height / metadata.ImageSize.Height);

        var boxes = new List<BoundingBox>(output.Dimensions[2]);

        Parallel.For(0, output.Dimensions[2], i =>
        {
            Parallel.For(0, metadata.Classes.Count, j =>
            {
                var confidence = output[0, j + 4, i];

                if (confidence <= parameters.Confidence)
                    return;

                var x = output[0, 0, i];
                var y = output[0, 1, i];
                var w = output[0, 2, i];
                var h = output[0, 3, i];

                var xMin = (int)((x - w / 2 - xPadding) * magnificationRatio);
                var yMin = (int)((y - h / 2 - yPadding) * magnificationRatio);
                var xMax = (int)((x + w / 2 - xPadding) * magnificationRatio);
                var yMax = (int)((y + h / 2 - yPadding) * magnificationRatio);

                xMin = Math.Clamp(xMin, 0, origin.Width);
                yMin = Math.Clamp(yMin, 0, origin.Height);
                xMax = Math.Clamp(xMax, 0, origin.Width);
                yMax = Math.Clamp(yMax, 0, origin.Height);

                var rectangle = Rectangle.FromLTRB(xMin, yMin, xMax, yMax);
                var name = metadata.Classes[j];

                var box = new BoundingBox(name, rectangle, confidence);
                boxes.Add(box);
            });
        });

        var selected = boxes.NonMaxSuppression(x => x.Rectangle,
                                               x => x.Confidence,
                                               _parameters.IoU);

        return selected;
    }
}