#include <iostream>
#include <string>
#include <unordered_map>

using std::string;
using std::unordered_map;

// O(n)
bool is_girl(const string& name) {
  unordered_map<char, bool> used;
  int unique = 0;

  for (char c : name) {
    if (used[c]) continue;
    used[c] = true;
    unique++;
  }

  return (unique % 2 == 0);
}

int main(int argc, char* argv[]) {
  string name;
  std::cin >> name;
  if (is_girl(name))
    std::cout << "CHAT WITH HER!"
              << "\n";
  else
    std::cout << "IGNORE HIM!"
              << "\n";

  return 0;
}
