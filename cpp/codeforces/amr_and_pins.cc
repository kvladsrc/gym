#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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

double square(double a) { return a * a; }

int main(int argc, char *argv[]) {
  int r, x, y, x_new, y_new;
  std::cin >> r >> x >> y >> x_new >> y_new;
  double dist = sqrt(square(abs(x_new - x)) + square(abs(y_new - y)));

  int res = dist / (2 * r);
  if (dist > (res * (2 * r))) res++;
  std::cout << res << "\n";

  return 0;
}
