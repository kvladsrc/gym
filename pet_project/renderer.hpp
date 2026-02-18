#ifndef PET_PROJECT_RENDERER_H_
#define PET_PROJECT_RENDERER_H_

#include <cstdint>
#include <string>
#include <vector>

#include "pet_project/types.hpp"

namespace evo {

class Renderer {
 public:
  Renderer(int width, int height, float blur_strength = 1.0f);

  std::vector<uint8_t> render(const DNA& dna) const;
  void save_png(const std::vector<uint8_t>& rgba,
                const std::string& path) const;

 private:
  int width_;
  int height_;
  float blur_strength_;
};

}  // namespace evo

#endif  // PET_PROJECT_RENDERER_H_
