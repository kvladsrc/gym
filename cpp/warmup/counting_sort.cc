#include "cpp/warmup/counting_sort.hpp"

#include <algorithm>
#include <cstddef>
#include <vector>

namespace warmup {

void counting_sort(std::vector<int>& v) {
  if (v.empty()) {
    return;
  }
  int max_val = 0;

  for (auto i : v) {
    max_val = std::max(i, max_val);

    if (i < 0) {
      return;
    }
  }

  std::vector<int> table(max_val + 1, 0);
  for (auto i : v) {
    table[i]++;
  }

  v.clear();
  for (std::size_t i = 0; i < table.size(); ++i) {
    while (table[i] != 0) {
      v.push_back(i);
      table[i]--;
    }
  }
}

}  // namespace warmup
