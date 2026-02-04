#include <algorithm>
#include <cctype>
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
  int t;
  std::cin >> t;
  while (t) {
    string input;
    std::cin >> input;
    std::transform(input.begin(), input.end(), input.begin(),
                   [](char c) { return std::tolower(c); });
    if (input == "yes")
      std::cout << "YES"
                << "\n";
    else
      std::cout << "NO"
                << "\n";
    t--;
  }

  return 0;
}
