#include "cpp/warmup/sparse_table.hpp"

#include <algorithm>
#include <vector>

namespace warmup {

sparse_table::sparse_table(const std::vector<int>& v) {
  if (v.empty()) {
    return;
  }
  table.emplace_back(v);

  size_t window = 2;
  size_t cur_power = 1;

  while (window <= v.size()) {
    table.emplace_back();
    table.back().resize(v.size() - window + 1);

    for (size_t i = 0; i + window - 1 < v.size(); ++i) {
      table[cur_power][i] = std::min(table[cur_power - 1][i],
                                     table[cur_power - 1][i + (window / 2)]);
    }
    cur_power++;
    window *= 2;
  }

  logs.resize(v.size() + 1);
  if (!logs.empty()) {
    logs[0] = 0;
  }

  if (logs.size() > 1) {
    logs[1] = 0;
  }

  for (size_t rl = 2; rl <= v.size(); ++rl) {
    logs[rl] = logs[rl / 2] + 1;
  }
}

int sparse_table::query(size_t l, size_t r) const {
  auto cur_power = logs[r - l + 1];
  return std::min(table[cur_power][l],
                  table[cur_power][r - (1 << cur_power) + 1]);
}

}  // namespace warmup
