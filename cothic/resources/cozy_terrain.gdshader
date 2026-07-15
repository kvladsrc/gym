shader_type spatial;
render_mode diffuse_burley;

uniform sampler2D palette_texture : source_color, filter_nearest;
uniform vec3 grass_key : source_color = vec3(0.2824, 0.7569, 0.6392);
uniform vec3 grass_dark_key : source_color = vec3(0.2392, 0.6824, 0.5725);
uniform vec3 sand_key : source_color = vec3(0.949, 0.749, 0.6);
uniform vec3 sand_bank_key : source_color = vec3(0.9451, 0.5922, 0.4235);
uniform vec3 stone_key : source_color = vec3(0.6275, 0.6588, 0.7882);
uniform vec3 water_key : source_color = vec3(0.5608, 0.8588, 1.0);
uniform vec3 bark_light_key : source_color = vec3(0.6706, 0.3725, 0.2549);
uniform vec3 bark_dark_key : source_color = vec3(0.5961, 0.349, 0.2549);
uniform float roof_enabled : hint_range(0.0, 1.0, 1.0) = 0.0;

uniform vec3 grass_shadow : source_color = vec3(0.17, 0.34, 0.19);
uniform vec3 grass_light : source_color = vec3(0.5, 0.72, 0.38);
uniform vec3 soil_shadow : source_color = vec3(0.17, 0.1, 0.04);
uniform vec3 soil_light : source_color = vec3(0.52, 0.36, 0.15);
uniform vec3 dry_grass : source_color = vec3(0.58, 0.53, 0.29);
uniform float grass_texels_per_unit : hint_range(1.0, 8.0, 0.5) = 5.0;
uniform float grass_grain_strength : hint_range(0.0, 0.25, 0.01) = 0.18;

uniform vec3 sand_shadow : source_color = vec3(0.52, 0.34, 0.18);
uniform vec3 sand_light : source_color = vec3(0.92, 0.76, 0.43);
uniform vec3 sand_warm : source_color = vec3(0.8, 0.49, 0.22);
uniform float sand_texels_per_unit : hint_range(1.0, 8.0, 0.5) = 4.5;
uniform float sand_grain_strength : hint_range(0.0, 0.3, 0.01) = 0.16;

uniform vec3 stone_shadow : source_color = vec3(0.17, 0.21, 0.23);
uniform vec3 stone_light : source_color = vec3(0.72, 0.73, 0.69);
uniform vec3 stone_warm : source_color = vec3(0.48, 0.44, 0.37);
uniform float stone_texels_per_unit : hint_range(1.0, 8.0, 0.5) = 5.0;
uniform float stone_grain_strength : hint_range(0.0, 0.4, 0.01) = 0.34;

uniform vec3 foliage_shadow : source_color = vec3(0.035, 0.09, 0.055);
uniform vec3 foliage_mid : source_color = vec3(0.1, 0.24, 0.12);
uniform vec3 foliage_light : source_color = vec3(0.27, 0.42, 0.18);
uniform vec3 foliage_warm : source_color = vec3(0.22, 0.31, 0.11);
uniform float foliage_texels_per_unit : hint_range(1.0, 10.0, 0.5) = 5.5;

uniform vec3 bark_shadow : source_color = vec3(0.2, 0.12, 0.07);
uniform vec3 bark_mid : source_color = vec3(0.43, 0.25, 0.12);
uniform vec3 bark_light : source_color = vec3(0.62, 0.4, 0.2);

uniform vec3 roof_cool_shadow : source_color = vec3(0.12, 0.15, 0.23);
uniform vec3 roof_cool_mid : source_color = vec3(0.29, 0.34, 0.48);
uniform vec3 roof_cool_light : source_color = vec3(0.55, 0.54, 0.61);
uniform float roof_texels_per_unit : hint_range(1.0, 10.0, 0.5) = 5.0;

uniform vec3 water_deep : source_color = vec3(0.08, 0.25, 0.32);
uniform vec3 water_mid : source_color = vec3(0.16, 0.42, 0.48);
uniform vec3 water_light : source_color = vec3(0.5, 0.76, 0.72);
uniform float water_texels_per_unit : hint_range(1.0, 8.0, 0.5) = 3.0;
uniform float water_animation_fps : hint_range(1.0, 12.0, 1.0) = 6.0;

varying vec3 world_position;
varying float model_height;


float random_value(vec2 point) {
	return fract(sin(dot(point, vec2(127.1, 311.7))) * 43758.5453);
}


float value_noise(vec2 point) {
	vec2 cell = floor(point);
	vec2 offset = fract(point);
	vec2 blend = offset * offset * (3.0 - 2.0 * offset);

	float bottom = mix(random_value(cell), random_value(cell + vec2(1.0, 0.0)), blend.x);
	float top = mix(
		random_value(cell + vec2(0.0, 1.0)),
		random_value(cell + vec2(1.0, 1.0)),
		blend.x
	);
	return mix(bottom, top, blend.y);
}


float palette_mask(vec3 color, vec3 key) {
	return 1.0 - smoothstep(0.015, 0.055, distance(color, key));
}


float grass_palette_mask(vec3 color) {
	float light = 1.0 - smoothstep(0.015, 0.075, distance(color, grass_key));
	float dark = 1.0 - smoothstep(0.015, 0.075, distance(color, grass_dark_key));
	// Composite tiles interpolate several greens from the shared palette.
	// Recognize the whole grass family while keeping blue water out.
	float green_family = smoothstep(0.025, 0.09, color.g - color.r);
	green_family *= smoothstep(0.015, 0.07, color.g - color.b);
	return max(max(light, dark), green_family);
}


float sand_palette_mask(vec3 color) {
	return max(palette_mask(color, sand_key), palette_mask(color, sand_bank_key));
}


vec2 warped_position(vec2 position, float frequency, float strength) {
	vec2 warp = vec2(
		value_noise(position * frequency),
		value_noise(position * frequency + vec2(37.0, 19.0))
	);
	return position + (warp - vec2(0.5)) * strength;
}


vec3 grass_surface(vec2 position) {
	vec2 pixel_cell = floor(position * grass_texels_per_unit);
	vec2 pixel_position = pixel_cell / grass_texels_per_unit;
	vec2 point = warped_position(pixel_position, 0.15, 2.4);
	float broad = value_noise(point * 0.2);
	float detail = value_noise(point * 0.78);
	float grain = value_noise(point * 1.9);
	float pixel_grain = random_value(pixel_cell);

	float brightness = 0.34 + broad * 0.28 + detail * 0.2 + grain * 0.08;
	brightness += (pixel_grain - 0.5) * grass_grain_strength;
	vec3 grass = mix(grass_shadow, grass_light, brightness);

	float patch_noise = value_noise(point * 0.34) * 0.72 + detail * 0.28;
	float soil_mask = smoothstep(0.74, 0.84, patch_noise);
	vec3 soil = mix(soil_shadow, soil_light, 0.35 + grain * 0.5);

	float dry_mask = smoothstep(0.84, 0.97, grain) * (1.0 - soil_mask);
	vec3 color = mix(grass, soil, soil_mask * 0.72);
	color = mix(color, dry_grass, dry_mask * 0.12);

	float light_fleck = smoothstep(0.86, 0.98, pixel_grain);
	float dark_fleck = 1.0 - smoothstep(0.02, 0.14, pixel_grain);
	color = mix(color, grass_light, light_fleck * 0.24);
	return mix(color, grass_shadow, dark_fleck * 0.22);
}


vec3 sand_surface(vec2 position) {
	vec2 pixel_cell = floor(position * sand_texels_per_unit);
	vec2 pixel_position = pixel_cell / sand_texels_per_unit;
	vec2 point = warped_position(pixel_position, 0.12, 1.8);
	float broad = value_noise(point * 0.24);
	float grain = value_noise(point * 1.35);
	float pixel_grain = random_value(pixel_cell);
	float brightness = 0.28 + broad * 0.38 + grain * 0.16;
	brightness += (pixel_grain - 0.5) * sand_grain_strength;
	vec3 color = mix(sand_shadow, sand_light, brightness);
	float warm_mask = smoothstep(0.76, 0.94, grain);
	color = mix(color, sand_warm, warm_mask * 0.2);
	float light_grain = smoothstep(0.88, 0.98, pixel_grain);
	float dark_grain = 1.0 - smoothstep(0.02, 0.12, pixel_grain);
	color = mix(color, sand_light, light_grain * 0.22);
	return mix(color, sand_shadow, dark_grain * 0.2);
}


vec3 stone_surface(vec2 position) {
	vec2 pixel_cell = floor(position * stone_texels_per_unit);
	vec2 pixel_position = pixel_cell / stone_texels_per_unit;
	vec2 point = warped_position(pixel_position, 0.17, 1.1);
	float broad = value_noise(point * 0.34);
	float detail = value_noise(point * 1.3);
	float chips = value_noise(point * 2.7);
	float pixel_grain = random_value(pixel_cell);
	float brightness = 0.18 + broad * 0.3 + detail * 0.22 + chips * 0.13;
	brightness += (pixel_grain - 0.5) * stone_grain_strength;
	vec3 color = mix(stone_shadow, stone_light, brightness);

	float warm_mask = smoothstep(0.76, 0.92, detail) * smoothstep(0.42, 0.7, broad);
	color = mix(color, stone_warm, warm_mask * 0.16);
	float light_chip = smoothstep(0.87, 0.98, pixel_grain);
	float dark_chip = 1.0 - smoothstep(0.02, 0.13, pixel_grain);
	color = mix(color, stone_light, light_chip * 0.44);
	return mix(color, stone_shadow, dark_chip * 0.5);
}


vec3 foliage_surface(vec3 position) {
	vec2 projected_position = vec2(
		position.x + position.y * 0.58,
		position.z - position.y * 0.43
	);
	vec2 pixel_cell = floor(projected_position * foliage_texels_per_unit);
	vec2 pixel_position = pixel_cell / foliage_texels_per_unit;
	float broad = value_noise(pixel_position * 0.18);
	float detail = value_noise(pixel_position * 1.15);
	float pixel_grain = random_value(pixel_cell);

	float brightness = 0.2 + broad * 0.48 + detail * 0.15;
	vec3 color = mix(foliage_shadow, foliage_mid, brightness);
	float light_leaf = smoothstep(0.74, 0.94, pixel_grain) * smoothstep(0.34, 0.64, broad);
	float dark_leaf = 1.0 - smoothstep(0.05, 0.24, pixel_grain);
	color = mix(color, foliage_light, light_leaf * 0.68);
	color = mix(color, foliage_shadow, dark_leaf * 0.5);

	float warm_leaf = smoothstep(0.8, 0.95, detail) * smoothstep(0.5, 0.74, broad);
	return mix(color, foliage_warm, warm_leaf * 0.38);
}


vec3 bark_surface(vec3 position) {
	vec2 projected_position = vec2(
		(position.x + position.z) * 7.0,
		position.y * 3.0
	);
	vec2 pixel_cell = floor(projected_position);
	float vertical_grain = value_noise(vec2(pixel_cell.x * 0.34, pixel_cell.y * 0.12));
	float chips = random_value(pixel_cell);
	vec3 color = mix(bark_shadow, bark_mid, 0.28 + vertical_grain * 0.58);
	float light_chip = smoothstep(0.82, 0.96, chips);
	float dark_chip = 1.0 - smoothstep(0.04, 0.18, chips);
	color = mix(color, bark_light, light_chip * 0.42);
	return mix(color, bark_shadow, dark_chip * 0.38);
}


float cool_roof_palette_mask(vec3 color) {
	// Separate saturated blue roofs from the palette's blue-gray stone shades.
	float blue_family = smoothstep(0.24, 0.34, color.b - color.r);
	blue_family *= smoothstep(0.16, 0.26, color.b - color.g);
	return blue_family;
}


vec3 roof_surface(vec3 position) {
	vec2 projected_position = vec2(
		position.x + position.y * 0.45,
		position.z - position.y * 0.35
	);
	vec2 pixel_cell = floor(projected_position * roof_texels_per_unit);
	vec2 pixel_position = pixel_cell / roof_texels_per_unit;
	float broad = value_noise(pixel_position * 0.22);
	float grain = random_value(pixel_cell);
	float brightness = 0.18 + broad * 0.5 + grain * 0.18;

	vec3 color = mix(roof_cool_shadow, roof_cool_mid, brightness);

	vec2 shingle_position = projected_position * 1.35;
	shingle_position.x += mod(floor(shingle_position.y), 2.0) * 0.5;
	vec2 shingle_uv = fract(shingle_position);
	float vertical_seam = 1.0 - step(0.08, shingle_uv.x);
	float horizontal_seam = 1.0 - step(0.13, shingle_uv.y);
	float seam = max(vertical_seam, horizontal_seam);
	color = mix(color, roof_cool_shadow, seam * 0.48);

	float light_chip = smoothstep(0.86, 0.98, grain);
	return mix(color, roof_cool_light, light_chip * 0.32);
}


vec3 water_surface(vec2 position) {
	float frame_time = floor(TIME * water_animation_fps) / water_animation_fps;
	vec2 broad_motion = vec2(frame_time * 0.28, frame_time * 0.11);
	vec2 detail_motion = vec2(-frame_time * 0.18, frame_time * 0.24);
	vec2 pixel_position = floor(position * water_texels_per_unit) / water_texels_per_unit;

	float broad = value_noise((pixel_position + broad_motion) * 0.18);
	float detail = value_noise((pixel_position + detail_motion) * 0.9);
	vec3 color = mix(water_deep, water_mid, 0.28 + broad * 0.58 + detail * 0.08);

	float broken_ripple = value_noise(pixel_position * 0.47 - detail_motion * 1.7);
	float ripple_mask = smoothstep(0.78, 0.92, detail) * smoothstep(0.45, 0.72, broad);
	ripple_mask *= smoothstep(0.35, 0.68, broken_ripple);
	float crest_wave = sin(
		pixel_position.x * 0.75 + pixel_position.y * 0.42 - frame_time * 1.25
	) * 0.5 + 0.5;
	float crest_breakup = value_noise(pixel_position * 0.39 + detail_motion * 2.2);
	float crest_mask = smoothstep(0.88, 0.98, crest_wave);
	crest_mask *= smoothstep(0.48, 0.7, crest_breakup);
	ripple_mask = max(ripple_mask, crest_mask * 0.75);
	return mix(color, water_light, ripple_mask * 0.58);
}


void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	model_height = VERTEX.y;
}


void fragment() {
	vec3 source = texture(palette_texture, UV).rgb;
	float ground_layer = 1.0 - smoothstep(0.205, 0.235, model_height);
	float grass = grass_palette_mask(source) * ground_layer;
	float sand = sand_palette_mask(source) * ground_layer;
	float stone = palette_mask(source, stone_key) * ground_layer;
	float water = palette_mask(source, water_key);
	float foliage = grass_palette_mask(source) * (1.0 - ground_layer);
	float bark_keys = max(
		palette_mask(source, bark_light_key),
		palette_mask(source, bark_dark_key)
	);
	float bark = bark_keys * (1.0 - ground_layer);
	float roof_height = step(0.23, model_height);
	float roof_palette = cool_roof_palette_mask(source) * roof_height;
	float roof = step(0.5, roof_palette) * roof_enabled;

	vec3 color = mix(source, grass_surface(world_position.xz), grass);
	color = mix(color, sand_surface(world_position.xz), sand);
	color = mix(color, stone_surface(world_position.xz), stone);
	color = mix(color, water_surface(world_position.xz), water);
	color = mix(color, foliage_surface(world_position), foliage);
	color = mix(color, bark_surface(world_position), bark);
	color = mix(color, roof_surface(world_position), roof);

	ALBEDO = color;
	ROUGHNESS = mix(0.92, 0.48, water);
	SPECULAR = mix(0.05, 0.42, water);
}
