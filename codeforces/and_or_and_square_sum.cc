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

  int n = 0;
  std::cin >> n;

  vector<int64_t> powers(21, 0);

  for (int i = 0; i < n; ++i) {
    int buf = 0;
    std::cin >> buf;
    int power = 0;
    while (buf != 0) {
      powers[power++] += buf % 2;
      buf /= 2;
    }
  }

  int64_t res = 0;
  for (int i = 0; i < n; ++i) {
    int64_t buf = 0;
    for (int p = 20; p >= 0; --p) {
      buf *= 2;
      if (powers[p] > 0) {
        buf++;
        powers[p]--;
      }
    }
    res += buf * buf;
  }

  std::cout << res << "\n";

  return 0;
}
