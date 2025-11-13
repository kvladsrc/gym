#include <algorithm>
#include <cstddef>
#include <cstdlib>
#include <ctime>
#include <functional>
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
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  string a;
  std::cin >> a;

  // F(2) = 2
  // F(3) = 2 * 3
  // F(4) = 2 * 3 * 4 = 2 * 3 * 2 * 2 = F(3) * 4 = F(3) * F(2) * F(2)
  // F(5) = 2 * 3 * 4 * 5
  // F(6) = 2 * 3 * 4 * 5 * 6 = F(5) * 6 = F(5) * F(3)
  // F(7) = 2 * 3 * 4 * 5 * 6 * 7
  // F(8) = 2 * 3 * 4 * 5 * 6 * 7 * 8 = F(7) * 8 = F(7) * F(2) * F(2) * F(2)
  // F(9) = 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 = F(7) * 8 * 9 = F(7) * 2 * 2 * 2 * 3
  // * 3 = F(7) * F(3) * F(3) * F(2)
  vector<int> res;
  for (char c : a) {
    int digit = c - '0';
    switch (digit) {
      case 2:
        res.push_back(2);
        break;
      case 3:
        res.push_back(3);
        break;
      case 4:
        res.push_back(2);
        res.push_back(2);
        res.push_back(3);
        break;
      case 5:
        res.push_back(5);
        break;
      case 6:
        res.push_back(5);
        res.push_back(3);
        break;
      case 7:
        res.push_back(7);
        break;
      case 8:
        res.push_back(7);
        res.push_back(2);
        res.push_back(2);
        res.push_back(2);
        break;
      case 9:
        res.push_back(7);
        res.push_back(3);
        res.push_back(3);
        res.push_back(2);
        break;
      default:
        // Skips 1 and 0
        break;
    }
  }

  // sort descending
  sort(res.begin(), res.end(), std::greater<int>());
  for (int i : res) {
    std::cout << i;
  }
  std::cout << std::endl;

  return 0;
}
