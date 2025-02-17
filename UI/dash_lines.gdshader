shader_type canvas_item;

void fragment()
{
    vec2 centre = UV - vec2(0.5);
    
    float totalEffect = 0.0; // Accumulator for multiple lines
    int numLines = 100; // Number of lines to generate

    for (int i = 0; i < numLines; i++)
    {
		// Distribute evenly around the circle
        float rotation_angle = (float(i) / float(numLines)) * TAU;

        // Rotation Matrix
        mat2 rotationMatrix = mat2(vec2(cos(rotation_angle), -sin(rotation_angle)),
                                   vec2(sin(rotation_angle), cos(rotation_angle)));

        // Rotate UV coordinates
        vec2 rotatedCentre = rotationMatrix * centre;

        // Compute angle from rotated coordinates
        float angle = atan(rotatedCentre.y, rotatedCentre.x);
        float count = 10.0; // Increase for denser lines

        float speedEffect = smoothstep(0.999, 1.0, sin(angle * count));

        totalEffect += speedEffect; // Accumulate the effect
    }

    // Normalize by number of lines
    totalEffect /= float(numLines);

    // Centre mask (Fades towards edges)
    float radius = length(centre);
    float mask = smoothstep(0.2, 0.7, radius);

    // Apply opacity instead of blackness
    COLOR = vec4(1.0, 1.0, 1.0, totalEffect * mask);
}
