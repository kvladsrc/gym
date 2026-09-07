shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
uniform sampler2D blurred_screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float pixel_size : hint_range(1.0, 8.0, 0.25) = 1.5;
uniform float color_levels : hint_range(4.0, 24.0, 1.0) = 16.0;
uniform float dither_strength : hint_range(0.0, 1.0) = 0.12;
uniform float saturation : hint_range(0.0, 1.5) = 0.88;
uniform float contrast : hint_range(0.5, 1.5) = 1.04;
uniform float tilt_shift_strength : hint_range(0.0, 1.0) = 0.75;
uniform float tilt_shift_focus_center : hint_range(0.0, 1.0) = 0.54;
uniform float tilt_shift_focus_width : hint_range(0.05, 0.5) = 0.15;
uniform float tilt_shift_transition : hint_range(0.05, 0.5) = 0.22;
uniform float tilt_shift_blur_radius : hint_range(0.0, 6.0) = 2.0;
uniform float tilt_shift_color_boost : hint_range(0.0, 0.3) = 0.07;
uniform vec3 shadow_tint : source_color = vec3(0.94, 0.97, 1.0);
uniform vec3 highlight_tint : source_color = vec3(1.08, 1.01, 0.86);


float bayer_2x2(vec2 position) {
	vec2 cell = mod(floor(position), 2.0);
	return mod(2.0 * cell.x + 3.0 * cell.y, 4.0);
}


float bayer_4x4(vec2 position) {
	return (4.0 * bayer_2x2(position) + bayer_2x2(floor(position * 0.5))) / 16.0;
}


float tilt_shift_amount(vec2 uv) {
	float focus_distance = abs(uv.y - tilt_shift_focus_center);
	return smoothstep(
		tilt_shift_focus_width,
		tilt_shift_focus_width + tilt_shift_transition,
		focus_distance
	) * tilt_shift_strength;
}


void fragment() {
	vec2 pixel_step = SCREEN_PIXEL_SIZE * pixel_size;
	vec2 sample_uv = (floor(SCREEN_UV / pixel_step) + vec2(0.5)) * pixel_step;
	float tilt_shift = tilt_shift_amount(sample_uv);
	vec3 sharp_color = textureLod(screen_texture, sample_uv, 0.0).rgb;
	vec3 blurred_color = textureLod(
		blurred_screen_texture, sample_uv, tilt_shift * tilt_shift_blur_radius
	).rgb;
	vec3 color = mix(sharp_color, blurred_color, smoothstep(0.0, 0.85, tilt_shift));

	float luminance = dot(color, vec3(0.2126, 0.7152, 0.0722));
	float miniature_boost = tilt_shift_color_boost * (0.45 + tilt_shift * 0.55);
	color = mix(vec3(luminance), color, saturation + miniature_boost);
	color += vec3(color.g * 0.025, 0.0, -color.g * 0.025);
	color *= mix(shadow_tint, highlight_tint, smoothstep(0.18, 0.82, luminance));
	color = (color - vec3(0.5)) * (contrast + miniature_boost * 0.45) + vec3(0.5);

	float dither = (bayer_4x4(FRAGCOORD.xy / pixel_size) - 0.5) * dither_strength;
	float level_count = color_levels - 1.0;
	color = floor(clamp(color, vec3(0.0), vec3(1.0)) * level_count + 0.5 + dither);
	color /= level_count;
	COLOR = vec4(color, 1.0);
}
