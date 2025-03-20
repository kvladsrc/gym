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

int round(int a, int b) {
  int score = 0;

  if (a > b) {
    score++;
  } else if (a < b) {
    score--;
  }

  return score;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int a1 = 0;
    int a2 = 0;
    int b1 = 0;
    int b2 = 0;
    std::cin >> a1 >> a2 >> b1 >> b2;

    int res = 0;

    if ((round(a1, b1) + round(a2, b2)) > 0) {
      res++;
    }

    if ((round(a1, b2) + round(a2, b1)) > 0) {
      res++;
    }

    if ((round(a2, b1) + round(a1, b2)) > 0) {
      res++;
    }

    if ((round(a2, b2) + round(a1, b1)) > 0) {
      res++;
    }

    std::cout << res << "\n";
  }

  return 0;
}
