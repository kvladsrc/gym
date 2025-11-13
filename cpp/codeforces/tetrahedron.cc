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

#define modulo 1000000007

// cur_v: 0 -> D, 1 -> A, 2 -> B, 3 -> C.
// O(3^steps_left)
int path_num_naive(int steps_left, int cur_v) {
  if (steps_left == 0) {
    if (cur_v) {
      return 0;
    } else {
      return 1;
    }
  }

  if (steps_left == 1) {
    if (cur_v) {
      return 1;
    } else {
      return 0;
    }
  }

  vector<bool> vertecies(4, true);
  vertecies[cur_v] = false;
  int res = 0;
  for (int i = 0; i < 4; ++i) {
    if (!vertecies[i]) {
      continue;
    }

    res += path_num_naive(steps_left - 1, i);
    res %= modulo;
  }

  return res;
}

// cur_v: 0 -> D, 1 -> A, 2 -> B, 3 -> C.
int path_num_dp(int steps_left) {
  vector<int> prev(4, 0);
  prev[0] = 1;

  for (int steps = 1; steps <= steps_left; ++steps) {
    vector<int> next(4, 0);
    for (int v = 0; v < 4; ++v) {
      for (int nv = 0; nv < 4; ++nv) {
        if (v == nv) {
          continue;
        }
        next[v] += prev[nv];
        next[v] %= modulo;
      }
    }
    prev = next;
  }

  return prev[0];
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  std::cout << path_num_dp(n) << "\n";

  return 0;
}
