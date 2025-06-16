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

int greedy(int r, int g, int b) {
  int res = 0;
  res += r / 3 + g / 3 + b / 3;
  r %= 3;
  g %= 3;
  b %= 3;
  res += min(r, min(g, b));
  return res;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int r = 0;
  int g = 0;
  int b = 0;
  std::cin >> r >> g >> b;

  int res = greedy(r, g, b);
  if (r != 0 && b != 0 && g != 0) {
    res = max(res, greedy(r - 1, g - 1, b - 1) + 1);
  }

  std::cout << res << "\n";

  return 0;
}
