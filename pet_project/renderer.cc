#include "pet_project/renderer.hpp"

#include <cmath>
#include <string>
#include <vector>

#define STB_IMAGE_WRITE_IMPLEMENTATION
#include "stb/stb_image_write.h"

namespace evo {

Renderer::Renderer(int width, int height, float blur_strength)
    : width_(width), height_(height), blur_strength_(blur_strength) {}

std::vector<uint8_t> Renderer::render(const DNA& dna) const {
  std::vector<uint8_t> buf(width_ * height_ * 4, 255);

  int grid_size = static_cast<int>(std::sqrt(dna.size()));
  int idx = 0;
  for (int gy = 0; gy < grid_size; gy++) {
    int y0 = gy * height_ / grid_size;
    int y1 = (gy + 1) * height_ / grid_size;
    for (int gx = 0; gx < grid_size; gx++) {
      int x0 = gx * width_ / grid_size;
      int x1 = (gx + 1) * width_ / grid_size;
      const auto& c = dna[idx++].color;
      for (int y = y0; y < y1; y++) {
        for (int x = x0; x < x1; x++) {
          int p = (y * width_ + x) * 4;
          buf[p] = c.r;
          buf[p + 1] = c.g;
          buf[p + 2] = c.b;
        }
      }
    }
  }

  if (blur_strength_ <= 0.0f) {
    return buf;
  }

  float s = blur_strength_;

  // 3x3 box blur blended with original: lerp(original, blurred, strength)
  std::vector<uint8_t> result(width_ * height_ * 4, 255);
  for (int y = 0; y < height_; y++) {
    for (int x = 0; x < width_; x++) {
      for (int c = 0; c < 3; c++) {
        int sum = 0;
        int count = 0;
        for (int dy = -1; dy <= 1; dy++) {
          for (int dx = -1; dx <= 1; dx++) {
            int ny = y + dy, nx = x + dx;
            if (ny >= 0 && ny < height_ && nx >= 0 && nx < width_) {
              sum += buf[(ny * width_ + nx) * 4 + c];
              count++;
            }
          }
        }
        int orig = buf[(y * width_ + x) * 4 + c];
        int blurred = sum / count;
        result[(y * width_ + x) * 4 + c] =
            static_cast<uint8_t>(orig + s * (blurred - orig) + 0.5f);
      }
      result[(y * width_ + x) * 4 + 3] = 255;
    }
  }

  return result;
}

void Renderer::save_png(const std::vector<uint8_t>& rgba,
                        const std::string& path) const {
  stbi_write_png(path.c_str(), width_, height_, 4, rgba.data(), width_ * 4);
}

}  // namespace evo
