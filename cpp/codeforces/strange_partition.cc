#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    int x = 0;
    std::cin >> n >> x;
    vector<int> a(n);

    int64_t maximal = 0;
    int64_t sum = 0;

    for (auto& i : a) {
      std::cin >> i;
      sum += i;
      maximal += i / x;
      if (i % x > 0) {
        maximal++;
      }
    }

    auto minimal = sum / x;
    if (sum % x > 0) {
      minimal++;
    }

    std::cout << minimal << " " << maximal << "\n";
  }

  return 0;
}
