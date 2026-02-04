#include <algorithm>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int k, r;
  std::cin >> k >> r;

  int showels = 0, num = 0;
  while (showels % 10 != r) {
    showels += k;
    num++;
    if (!(showels % 10)) break;
  }
  std::cout << num << "\n";

  return 0;
}
