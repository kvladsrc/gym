#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  std::cin >> n;
  vector<int64_t> a(n);
  int64_t maximal = INT64_MIN;
  for (auto& i : a) {
    std::cin >> i;
    maximal = max(maximal, i);
  }

  int64_t free = std::accumulate(
      a.begin(), a.end(), static_cast<int64_t>(0),
      [maximal](int64_t acc, int64_t i) { return acc + maximal - i; });

  int64_t res = min(maximal, free);
  int64_t d = max(static_cast<int64_t>(0), maximal - free);

  res += (d / (n - 1)) * n;
  if (d % (n - 1) != 0) {
    res += (d % (n - 1));
    res++;
  }

  std::cout << res << "\n";

  return 0;
}
