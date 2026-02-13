#include "pet_project/renderer.hpp"

#include <algorithm>
#include <cmath>

#define STB_IMAGE_WRITE_IMPLEMENTATION
#include "stb/stb_image_write.h"

namespace evo {

Renderer::Renderer(int width, int height) : width_(width), height_(height) {}

std::vector<uint8_t> Renderer::render(const DNA& dna) const {
  // RGBA buffer, white background
  std::vector<uint8_t> buf(width_ * height_ * 4, 255);

  for (const auto& gene : dna) {
    if (!gene.enabled) {
      continue;
    }

    // Convert normalized coords to pixel coords
    float px[3], py[3];
    for (int i = 0; i < 3; i++) {
      px[i] = gene.vertices[i].x * (width_ - 1);
      py[i] = gene.vertices[i].y * (height_ - 1);
    }

    // Bounding box
    int min_x = std::max(
        0, static_cast<int>(std::floor(std::min({px[0], px[1], px[2]}))));
    int max_x =
        std::min(width_ - 1,
                 static_cast<int>(std::ceil(std::max({px[0], px[1], px[2]}))));
    int min_y = std::max(
        0, static_cast<int>(std::floor(std::min({py[0], py[1], py[2]}))));
    int max_y =
        std::min(height_ - 1,
                 static_cast<int>(std::ceil(std::max({py[0], py[1], py[2]}))));

    // Precompute edge function denominators
    float d =
        (py[1] - py[2]) * (px[0] - px[2]) + (px[2] - px[1]) * (py[0] - py[2]);
    if (std::abs(d) < 1e-6f) continue;  // degenerate triangle
    float inv_d = 1.0f / d;

    float alpha_src = gene.color.a / 255.0f;

    for (int y = min_y; y <= max_y; y++) {
      for (int x = min_x; x <= max_x; x++) {
        // Barycentric coordinates
        float w0 =
            ((py[1] - py[2]) * (x - px[2]) + (px[2] - px[1]) * (y - py[2])) *
            inv_d;
        float w1 =
            ((py[2] - py[0]) * (x - px[2]) + (px[0] - px[2]) * (y - py[2])) *
            inv_d;
        float w2 = 1.0f - w0 - w1;

        if (w0 >= 0 && w1 >= 0 && w2 >= 0) {
          int idx = (y * width_ + x) * 4;
          // Source-over alpha compositing
          float dst_a = buf[idx + 3] / 255.0f;
          float out_a = alpha_src + dst_a * (1.0f - alpha_src);

          if (out_a > 0) {
            buf[idx + 0] = static_cast<uint8_t>(
                (gene.color.r * alpha_src +
                 buf[idx + 0] * dst_a * (1.0f - alpha_src)) /
                out_a);
            buf[idx + 1] = static_cast<uint8_t>(
                (gene.color.g * alpha_src +
                 buf[idx + 1] * dst_a * (1.0f - alpha_src)) /
                out_a);
            buf[idx + 2] = static_cast<uint8_t>(
                (gene.color.b * alpha_src +
                 buf[idx + 2] * dst_a * (1.0f - alpha_src)) /
                out_a);
            buf[idx + 3] = static_cast<uint8_t>(out_a * 255.0f);
          }
        }
      }
    }
  }

  return buf;
}

void Renderer::save_png(const std::vector<uint8_t>& rgba,
                        const std::string& path) const {
  stbi_write_png(path.c_str(), width_, height_, 4, rgba.data(), width_ * 4);
}

}  // namespace evo
