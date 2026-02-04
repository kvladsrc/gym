#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdarg>
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

// 1  2  4  7  11
// 3  5  8  12
// 6  9  13
// 10 14
// 15
//
// 1 3 6 10
int64_t sum_of_first(int64_t n) { return ((n + 1) * n) / 2; }

int64_t value(int x, int y) {
  int64_t res = 0;
  res += sum_of_first(y);
  res += sum_of_first(x - 1) + ((y - 1) * (x - 1));
  return res;
}

int64_t factorial(int64_t a) {
  int64_t res = 1;
  while (a) {
    res *= a--;
  }

  return res;
}

int64_t min_path(int64_t x, int64_t y, int64_t dest_x, int64_t dest_y) {
  int64_t res = value(dest_x, dest_y);
  while (x < dest_x) {
    res += value(x++, y);
  }
  while (y < dest_y) {
    res += value(x, y++);
  }
  return res;
}

int64_t max_path(int64_t x, int64_t y, int64_t dest_x, int64_t dest_y) {
  int64_t res = value(dest_x, dest_y);
  while (y < dest_y) {
    res += value(x, y++);
  }
  while (x < dest_x) {
    res += value(x++, y);
  }
  return res;
}

// Result is the number of different values from max_path to min_path,
// where max_path is the path from (x,y) to (x, dest_y) and then to
// (dest_x, dest_y) and min path is the path from (x, y) to right and
// then down.
//
// The following pathes are differ by one: right, down and down,
// right. So on the rectangle (x, y) - (dest_x, dest_y) two paths with
// differ in one sqare differ by 1. So we can count just a number of
// squares.
int64_t fast_solution(int64_t x, int64_t y, int64_t dest_x, int64_t dest_y) {
  auto w = dest_x - x;
  auto h = dest_y - y;
  return w * h + 1;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int x, y, nx, ny;
    std::cin >> x >> y >> nx >> ny;
    std::cout << fast_solution(x, y, nx, ny) << "\n";
  }

  return 0;
}
