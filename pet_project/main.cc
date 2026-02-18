#include <chrono>
#include <csignal>
#include <cstdio>
#include <cstdlib>
#include <filesystem>
#include <string>

#include "pet_project/config.hpp"
#include "pet_project/evolution.hpp"
#include "pet_project/fitness.hpp"
#include "pet_project/renderer.hpp"

using std::chrono::duration;
using std::chrono::high_resolution_clock;

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

  evo::Renderer renderer(config.image_width, config.image_height,
                         config.blur_strength);
  evo::Fitness fitness(config);
  evo::EvolutionEngine engine(config, renderer, fitness);

  engine.init_population();

  std::signal(SIGINT, signal_handler);

  std::printf(
      "Starting evolution (population=%d, initial_grid=%dx%d->%dx%d, "
      "class=%d)\n",
      config.population_size, config.initial_grid_size,
      config.initial_grid_size, config.max_grid_size, config.max_grid_size,
      config.imagenet_class);

  while (g_running && !engine.finished()) {
    int gen = engine.generation();

    auto step_start = high_resolution_clock::now();
    engine.step();
    auto step_end = high_resolution_clock::now();

    if ((gen + 1) % config.print_interval == 0) {
      duration<float, std::milli> step_time = step_end - step_start;
      std::printf("Gen %d [%dx%d]: fitness=%.9f, time=%.1f ms\n", gen + 1,
                  engine.current_grid_size(), engine.current_grid_size(),
                  engine.best().fitness, step_time.count());
      std::fflush(stdout);
    }

    if ((gen + 1) % config.save_interval == 0) {
      auto image = renderer.render(engine.best().dna);
      char fname[64];
      std::snprintf(fname, sizeof(fname), "/gen_%07d.png", gen + 1);
      auto path = config.output_dir + fname;
      renderer.save_png(image, path);
      std::printf("Saved %s\n", path.c_str());
    }
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
