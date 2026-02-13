#include <csignal>
#include <cstdio>
#include <cstdlib>
#include <filesystem>
#include <string>

#include "pet_project/config.hpp"
#include "pet_project/evolution.hpp"
#include "pet_project/fitness.hpp"
#include "pet_project/renderer.hpp"

namespace {
volatile sig_atomic_t g_running = 1;
void signal_handler(int) { g_running = 0; }
}  // namespace

int main(int argc, char* argv[]) {
  if (argc < 2) {
    std::fprintf(stderr, "Usage: %s <config.json>\n", argv[0]);
    return 1;
  }

  auto config = evo::Config::load(argv[1]);

  std::filesystem::create_directories(config.output_dir);

  evo::Renderer renderer(config.image_width, config.image_height);
  evo::Fitness fitness(config);
  evo::EvolutionEngine engine(config, renderer, fitness);

  engine.init_population();

  std::signal(SIGINT, signal_handler);

  std::printf("Starting evolution (population=%d, dna_length=%d, class=%d)\n",
              config.population_size, config.dna_length, config.imagenet_class);

  while (g_running) {
    int gen = engine.generation();

    if (gen % 10 == 0) {
      std::printf("Gen %d: fitness=%.9f\n", gen, engine.best().fitness);
      std::fflush(stdout);
    }

    if (gen % 100 == 0) {
      auto image = renderer.render(engine.best().dna);
      auto path = config.output_dir + "/gen_" + std::to_string(gen) + ".png";
      renderer.save_png(image, path);
      std::printf("Saved %s\n", path.c_str());
    }

    engine.step();
  }

  // Save final result
  auto image = renderer.render(engine.best().dna);
  auto path = config.output_dir + "/final_gen_" +
              std::to_string(engine.generation()) + ".png";
  renderer.save_png(image, path);
  std::printf("\nStopped at gen %d. Saved %s\n", engine.generation(),
              path.c_str());

  return 0;
}
