#ifndef CPP_WARMUP_A_STAR_H_
#define CPP_WARMUP_A_STAR_H_

#include <vector>

namespace warmup {

using Maze = std::vector<std::vector<char>>;

struct cell {
  int x, y;
};

bool a_star_search(Maze& maze, const cell& start, const cell& end);

}  // namespace warmup

#endif  // CPP_WARMUP_A_STAR_H_
