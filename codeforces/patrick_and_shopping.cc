#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int d1, d2, d3;
  std::cin >> d1 >> d2 >> d3;

  int res;
  if (d3 > (d1 + d2)) {
    res = 2 * d1 + 2 * d2;
  } else {
    int case1 = d1 + d3 + d2;
    int case2 = 2 * d1 + 2 * d3;
    int case3 = 2 * d2 + 2 * d3;
    res = min(case1, min(case2, case3));
  }
  std::cout << res << "\n";

  return 0;
}
