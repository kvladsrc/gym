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

typedef vector<vector<int>> field;

int main(int argc, char *argv[]) {
  int n, x_0, y_0;
  std::cin >> n >> x_0 >> y_0;

  map<double, bool> c_map;
  bool on_y = false;
  for (int i = 0; i < n; ++i) {
    int x, y;
    std::cin >> x >> y;
    if (y != y_0) {
      double c = double(x - x_0) / double(y - y_0);
      c_map[c] = true;
    } else {
      on_y = true;
    }
  }

  auto res = c_map.size();
  if (on_y)
    res++;
  std::cout << res << "\n";

  return 0;
}
