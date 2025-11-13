#include "cpp/warmup/partition.hpp"

#include <cstddef>
#include <utility>
#include <vector>

namespace warmup {

int partition_to_right(std::vector<int>& a) {
  if (a.empty()) {
    return -1;
  }

  int const pivot = a.back();
  int less = -1;
  std::size_t more = 0;
  while (more < a.size() - 1) {
    if (a[more] < pivot) {
      std::swap(a[++less], a[more]);
    }
    more++;
  }
  std::swap(a[++less], a.back());

  return less;
}

int partition_to_mid(std::vector<int>& a) {
  if (a.empty()) {
    return -1;
  }

  auto pivot = a.back();
  int l = 0;
  int r = a.size() - 2;
  while (l <= r) {
    if (a[l] < pivot) {
      l++;
      continue;
    }
    if (a[r] >= pivot) {
      r--;
      continue;
    }
    std::swap(a[l], a[r]);
  }

  std::swap(a[l], a.back());
  return l;
}

}  // namespace warmup
