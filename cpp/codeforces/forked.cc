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
    int a = 0;
    int b = 0;
    std::cin >> a >> b;

    int xk = 0;
    int yk = 0;
    std::cin >> xk >> yk;

    int xq = 0;
    int yq = 0;
    std::cin >> xq >> yq;

    map<int, map<int, bool>> moves;
    int res = 0;

    moves[xk - a][yk - b] = true;
    moves[xk - a][yk + b] = true;
    moves[xk + a][yk - b] = true;
    moves[xk + a][yk + b] = true;
    moves[xk - b][yk - a] = true;
    moves[xk - b][yk + a] = true;
    moves[xk + b][yk - a] = true;
    moves[xk + b][yk + a] = true;

    if (moves[xq - a][yq - b]) {
      res++;
    }

    if (moves[xq - a][yq + b]) {
      res++;
    }

    if (moves[xq + a][yq - b]) {
      res++;
    }

    if (moves[xq + a][yq + b]) {
      res++;
    }

    if (moves[xq - b][yq - a]) {
      res++;
    }

    if (moves[xq - b][yq + a]) {
      res++;
    }

    if (moves[xq + b][yq - a]) {
      res++;
    }

    if (moves[xq + b][yq + a]) {
      res++;
    }

    std::cout << res << "\n";
  }

  return 0;
}
