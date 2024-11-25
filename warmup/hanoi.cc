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

// Move n disks from first to third using second.
int hanoi(int first, int second, int third, int n) {
  int res = 0;

  if (n == 0) {
    return res;
  }

  res += hanoi(first, third, second, n - 1);
  std::cout << "Move disk from: " << first << " to " << third << "\n";
  res++;
  res += hanoi(second, first, third, n - 1);

  return res;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  auto moves = hanoi(1, 2, 3, n);

  std::cout << "Number of moves: " << moves << "\n";

  return 0;
}
