#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

struct point {
  int x, y;
};

int main(int argc, char* argv[]) {
  point first, second;
  std::cin >> first.x >> first.y >> second.x >> second.y;

  if (first.x == second.x) {
    int side = abs(first.y - second.y);

    std::cout << first.x + side << " ";
    std::cout << first.y << " ";
    std::cout << second.x + side << " ";
    std::cout << second.y << "\n";

    return 0;
  }

  if (first.y == second.y) {
    int side = abs(first.x - second.x);

    std::cout << first.x << " ";
    std::cout << first.y + side << " ";
    std::cout << second.x << " ";
    std::cout << second.y + side << "\n";

    return 0;
  }

  if (abs(first.x - second.x) != abs(first.y - second.y)) {
    std::cout << -1 << "\n";
    return 0;
  }

  std::cout << first.x << " ";
  std::cout << second.y << " ";
  std::cout << second.x << " ";
  std::cout << first.y << "\n";

  return 0;
}
