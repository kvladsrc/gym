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
  int n;
  std::cin >> n;

  vector<int> q(5);
  for (int i = 0; i < 5; ++i) {
    q[i] = 1;
  }

  int next = 0;
  while (n) {
    n -= min(n, q[next]);
    q[next] *= 2;
    next = (next + 1) % 5;
  }

  switch (next - 1) {
    case 0:
      std::cout << "Sheldon"
                << "\n";
      break;
    case 1:
      std::cout << "Leonard"
                << "\n";
      break;
    case 2:
      std::cout << "Penny"
                << "\n";
      break;
    case 3:
      std::cout << "Rajesh"
                << "\n";
      break;
    default:
      std::cout << "Howard"
                << "\n";
  }

  return 0;
}
