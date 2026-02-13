#ifndef PET_PROJECT_FITNESS_H_
#define PET_PROJECT_FITNESS_H_

#include <onnxruntime_cxx_api.h>

#include <memory>
#include <vector>

#include "pet_project/config.hpp"
#include "pet_project/renderer.hpp"
#include "pet_project/types.hpp"

namespace evo {

class Fitness {
 public:
  explicit Fitness(const Config& config);

  float evaluate(const std::vector<uint8_t>& rgba, int width, int height);
  void evaluate_population(std::vector<Individual>& population,
                           const Renderer& renderer);

 private:
  std::vector<float> preprocess(const std::vector<uint8_t>& rgba, int src_w,
                                int src_h);

  float run_model(Ort::Session& session, const std::vector<float>& batch);

  Config config_;
  Ort::Env env_;
  std::vector<std::unique_ptr<Ort::Session>> sessions_;
  Ort::AllocatorWithDefaultOptions allocator_;
};

}  // namespace evo

#endif  // PET_PROJECT_FITNESS_H_
