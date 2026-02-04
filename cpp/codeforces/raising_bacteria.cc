#include <algorithm>
#include <cstdlib>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int x;
  std::cin >> x;
  int count = 0;
  while (x > 0) {
    if (x % 2 == 1) {
      count++;
    }
    x /= 2;
  }
  std::cout << count << "\n";

  return 0;
}
