#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

#define MAX_X 2000
#define MIN_X -2000
#define MAX_Y 2000
#define MIN_Y -2000

struct point {
  int x, y;
};

struct square {
  point bottom_left;
  point upper_right;

  int area();
};

int square::area() {
  return abs(upper_right.y - bottom_left.y) *
         abs(upper_right.x - bottom_left.x);
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int leftmost_x = MAX_X;
    int rightmost_x = MIN_X;
    int uppermost_y = MIN_Y;
    int bottommost_y = MAX_Y;

    for (int i = 0; i < 4; ++i) {
      int x, y;
      std::cin >> x >> y;
      if (x < leftmost_x) leftmost_x = x;
      if (x > rightmost_x) rightmost_x = x;
      if (y < bottommost_y) bottommost_y = y;
      if (y > uppermost_y) uppermost_y = y;
    }

    point bottom_left, upper_right;
    bottom_left.x = leftmost_x;
    bottom_left.y = bottommost_y;
    upper_right.x = rightmost_x;
    upper_right.y = uppermost_y;

    square s;
    s.upper_right = upper_right;
    s.bottom_left = bottom_left;

    std::cout << s.area() << "\n";
  }
  return 0;
}
