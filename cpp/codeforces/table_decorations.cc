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
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  vector<int> colors(3);
  std::cin >> colors[0] >> colors[1] >> colors[2];
  sort(colors.begin(), colors.end());

  int triple = colors.front();
  colors[0] -= triple;
  colors[1] -= triple;
  colors[2] -= triple;

  int delta = colors[2] - colors[1];
  int pairs = min(delta, colors[1]);
  colors[1] -= pairs;
  colors[2] -= pairs * 2;
  if (colors[1]) {
    auto d = colors[1] / 3;
    pairs += d * 2;
    colors[1] -= d * 3;
    colors[2] -= d * 3;
  }

  auto res = triple + pairs + min((colors[2] + colors[1]) / 3, triple);
  std::cout << res << "\n";

  return 0;
}
