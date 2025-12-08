#ifndef CPP_WARMUP_GRAPH_HEURISTIC_PAINTING_H_
#define CPP_WARMUP_GRAPH_HEURISTIC_PAINTING_H_

#include <map>
#include <vector>

namespace warmup {

using graph = std::vector<std::vector<int>>;

std::map<int, int> heuristic_painting(int n, const graph& g,
                                      int& max_color_out);

}  // namespace warmup

#endif  // CPP_WARMUP_GRAPH_HEURISTIC_PAINTING_H_
